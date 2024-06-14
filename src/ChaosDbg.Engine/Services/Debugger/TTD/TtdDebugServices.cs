using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ChaosDbg.Debugger;
using ChaosLib;
using ClrDebug.TTD;

namespace ChaosDbg.TTD
{
    public abstract class TtdDebugServices
    {
        public abstract ReplayEngine ReplayEngine { get; }

        #region GetFunctionCalls

        /// <summary>
        /// Gets all occurrences where one or more functions were called.
        /// </summary>
        /// <param name="addresses">The function addresses to resolve.</param>
        /// <param name="getName">A callback that can be used to resolve the name of each function.</param>
        /// <returns></returns>
        protected unsafe TtdRawFunctionCall[] GetFunctionCalls(long[] addresses, Func<long, string> getName)
        {
            var replayEngine = ReplayEngine;

            var cursor = replayEngine.NewCursor();

            /* How does TTDAnalyze collecting function calls, especially when there might be multiple functions to match for and even
             * multiple occurrences of each function?
             *
             * The steps it takes are as follows:
             *
             * 1. Create a new cursor for enumerating the function entry positions
             * 2. Add a memory watchpoint for every function entry point all at once
             * 3. ReplayForward, collecting the details of each function invocation in a memory watchpoint callback, including:
             *    - the return address of the function
             *    - the KSYSTEM_TIME that the function was invoked
             *    - possibly even parameter information as well?
             *
             * 4. Create a new cursor for enumerating the function exit positions
             * 5. Add a memory watchpoint for every distinct function return address all at once
             * 6. ReplayForward, collecting the details at the end of each function in a memory watchpoint callback, including:
             *    - the KSYSTEM_TIME that the function ended
             *    - possibly even the return value of the function
             *
             * 7. Sort the collected events in order of lowest position to highest position
             * 8. For each event that we couldn't retrieve the KSYSTEM_TIME for during our memory watchpoint callback, manually seek
             *    to each of these event positions and pull the KSYSTEM_TIME out of the process memory
             *
             * We roughly follow this same process, with some minor adjustments (e.g. we might use the same Cursor for both the entry and exit searches)
             * and may not collect all of the same info TTDAnalyze does in its memory watchpoint callback (such as parameters and return values)
             */

            //I don't know whether you can read a 32-bit trace in a 64-bit process, but for now we'll assume that is the case
            var entryPoints = TraceCodeExecutions<(bool isEntry, long returnAddress)>(cursor, addresses, threadView =>
            {
                var stackPointer = threadView.StackPointer;

                //TTDAnalyzer hardcoded the fact it wanted 8 bytes in the x64 version of TTDAnalyzer.dll. I don't know if the 32-bit
                //version will do the same thing
                var returnAddress = threadView.QueryMemoryBuffer<IntPtr>(stackPointer);

                return (true, (long) (void*) returnAddress);
            });

            //Now get the exit points
            var distinctExitAddresses = entryPoints.Select(e => e.Context.returnAddress).Distinct().ToArray();
            var exitPoints = TraceCodeExecutions<(bool isEntry, long returnAddress)>(cursor, distinctExitAddresses, _ => (false, default));

            //Sort all the events from the smallest to largest position so that we can marry up the start and end position of each function
            var allEvents = entryPoints.Concat(exitPoints).OrderBy(e => e.Position).ToArray();

            var results = FinalizeEvents(cursor, allEvents, getName);

            return results;
        }

        private TtdRawFunctionCall[] FinalizeEvents(Cursor cursor, TtdMemoryTraceResult<(bool isEntry, long returnAddress)>[] allEvents, Func<long, string> getName)
        {
            //A given function invocation could potentially call *another* function invocation. As such, we must keep track of
            //which function we're in

            var stack = new Stack<TtdRawFunctionCall>();
            var results = new List<TtdRawFunctionCall>();

            foreach (var @event in allEvents)
            {
                var isEntry = @event.Context.isEntry;

                if (@event.DateTime == null)
                {
                    //Last chance to get the time the event occurred!
                    cursor.SetPosition(@event.Position);
                    @event.DateTime = GetSystemTime(cursor);
                }

                if (isEntry)
                {
                    var call = new TtdRawFunctionCall(
                        getName(@event.Address),
                        @event.Address,
                        @event.Position,
                        @event.DateTime.Value,
                        @event.Context.returnAddress,
                        @event.Thread.ThreadId,
                        @event.Thread.UniqueThreadId
                    );

                    stack.Push(call);
                }
                else
                {
                    if (stack.Count == 0)
                    {
                        //I've observed in cases where a function never ended, you may get a bunch of noise where there was a hit on the end address at
                        //kernel32!BaseThreadInitThunk but there was no corresponding start address. While I would like to include every event for completeness,
                        //this is basically just noise, so instead we say, if there was no start, exclude the event

                        continue;
                    }
                    else
                    {
                        var call = stack.Pop();

                        call.EndPosition = @event.Position;
                        call.EndDateTime = @event.DateTime.Value;

                        results.Add(call);
                    }
                }
            }

            while (stack.Count > 0)
            {
                var call = stack.Pop();

                call.EndPosition = Position.Max;

                results.Add(call);
            }

            results.Sort((a, b) => a.StartPosition.CompareTo(b.StartPosition));

            return results.ToArray();
        }

        #endregion

        private unsafe TtdMemoryTraceResult<T>[] TraceCodeExecutions<T>(Cursor cursor, long[] addresses, Func<ThreadView, T> createContext)
        {
            //Even if you've just created a brand new cursor, you still have to set the initial position or things won't work
            cursor.SetPosition(Position.Min);

            var watchpoints = new List<MemoryWatchpointData>();

            foreach (var address in addresses)
            {
                var watchpoint = new MemoryWatchpointData
                {
                    address = address,
                    size = 1,
                    flags = BP_FLAGS.EXEC,
                };

                watchpoints.Add(watchpoint);

                cursor.AddMemoryWatchpoint(watchpoint);
            }

            var results = new ConcurrentBag<TtdMemoryTraceResult<T>>();

            cursor.SetMemoryWatchpointCallback((context, data, threadView) =>
            {
                var dateTime = GetCurrentTime(threadView);

                var result = new TtdMemoryTraceResult<T>(
                    dateTime,
                    data->address,
                    threadView.Position,
                    threadView.ThreadInfo,
                    data->flags,
                    createContext == null ? default : createContext(threadView)
                );

                results.Add(result);

                //Return true to stop, false to continue
                return false;
            });

            var replayResult = cursor.ReplayForward(Position.Max, StepCount.Max);

            //Now remove each of our watchpoints
            foreach (var watchpoint in watchpoints)
                cursor.RemoveMemoryWatchpoint(watchpoint);

            return results.ToArray();
        }

        private object ll = new object();

        private DateTime? GetCurrentTime(ThreadView threadView)
        {
            /* Get ready for some crazy hacks: you can query the current time from any point in a processes' lifetime by
             * taking advantage of the KUSER_SHARED_DATA data structure, which is loaded into address 0x7ffe0000 of every process.
             * At offset 0x14 of this struct is the "KSYSTEM_TIME SystemTime" field. */
            
            //Sometimes, values we're interested in can't be read from the ThreadView. This is normal. In the event that this happens, we can circle back
            //to each position we're missing values for at the end, and use our Cursor to read the memory instead
            if (!threadView.TryQueryMemoryBuffer<KSYSTEM_TIME>(WellKnownMemoryAddress.UserMode_KUSER_SHARED_DATA_SystemTime, out var systemTime))
                return null;

            /* From https://www.geoffchappell.com/studies/windows/km/ntoskrnl/inc/api/ntexapi_x/kuser_shared_data/index.htm
             *
             * The design of the KSYSTEM_TIME is that the 64-bit value is followed by a duplicate of its high part. The kernel
             * always writes the second high part and only then the usual low and high parts. Except for the kernel itself when
             * it knows it cannot be interrupted, readers of the 64-bit value follow by reading the second high part and checking
             * for equality with the first: if they differ, the reader knows to retry. */

            //When polling the KSYSTEM_TIME on a live system, you should keep querying the SystemTime and storing its high and low parts
            //in local variables until High1Time and High2Time match. In our case however, we're reading from a dump, so we'll go right ahead
            //and consume the low and high parts

            var fileTime = ((long) systemTime.High1Time) << 32 | (long) systemTime.LowPart;

            var time = DateTime.FromFileTime(fileTime);

            return time;
        }

        private DateTime? GetSystemTime(Cursor cursor)
        {
            if (!cursor.TryQueryMemoryBuffer<KSYSTEM_TIME>(WellKnownMemoryAddress.UserMode_KUSER_SHARED_DATA_SystemTime, QueryMemoryPolicy.Default, out var systemTime))
                return null;

            var fileTime = ((long) systemTime.High1Time) << 32 | (long) systemTime.LowPart;

            var time = DateTime.FromFileTime(fileTime);

            return time;
        }
    }
}
