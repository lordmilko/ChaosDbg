using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ChaosDbg.Disasm;
using ChaosDbg.Symbol;
using ClrDebug;
using Iced.Intel;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a store used to manage and provide access to breakpoints that have been set for the current process.
    /// </summary>
    public class CordbBreakpointStore : IEnumerable<CordbBreakpoint>
    {
        private object breakpointLock = new object();

        private Dictionary<ICorDebugFunctionBreakpoint, CordbManagedCodeBreakpoint> managedCodeBreakpoints = new();
        private Dictionary<CORDB_ADDRESS, CordbNativeCodeBreakpoint> nativeCodeBreakpoints = new();
        private Dictionary<CORDB_ADDRESS, CordbDataBreakpoint> dataBreakpoints = new();

        //When we hit a normal breakpoint, we need to replace the 0xcc with the original code byte, and then
        //set a breakpoint on the next instruction (if one doesn't already exist) and then reinstate this breakpoint
        private CordbNativeCodeBreakpoint deferredBreakpoint;
        private CordbNativeCodeBreakpoint currentBreakpoint;

        private CordbStepBreakpoint stepBreakpoint;

        private CordbProcess process;

        public CordbBreakpointStore(CordbProcess process)
        {
            this.process = process;
            stepBreakpoint = new CordbStepBreakpoint(process);
        }

        public CordbManagedCodeBreakpoint Add(CorDebugFunctionBreakpoint corDebugFunctionBreakpoint)
        {
            var breakpoint = new CordbManagedCodeBreakpoint(corDebugFunctionBreakpoint, false);

            lock (breakpointLock)
                managedCodeBreakpoints.Add(corDebugFunctionBreakpoint.Raw, breakpoint);

            return breakpoint;
        }

        public CordbNativeCodeBreakpoint Add(CORDB_ADDRESS address)
        {
            if (!process.Session.IsInterop)
                throw new InvalidOperationException("Adding breakpoints by address is only supported when interop debugging");

            CordbNativeCodeBreakpoint breakpoint;

            process.Symbols.TrySymFromAddr(address, SymFromAddrOption.All, out var symbol);

            //Is this breakpoint inside the CLR?
            if (process.Modules.TryGetModuleForAddress(address, out var module) && module is CordbNativeModule { IsCLR: true })
            {
                //In order to set breakpoints inside the CLR we need to set the breakpoint ourselves. See the comments in CordbBreakpoint.cs
                //for further details
                breakpoint = new CordbRawCodeBreakpoint(symbol.ToString(), address, process, false);
            }
            else
            {
                breakpoint = new CordbNativeCodeBreakpoint(symbol.ToString(), address, process, false);
            }

            breakpoint.SetEnabled(true);

            lock (breakpointLock)
                nativeCodeBreakpoints.Add(address, breakpoint);

            return breakpoint;
        }

        public CordbDataBreakpoint Add(long address, DR7.Kind accessKind, DR7.Length size)
        {
            var breakpoint = new CordbDataBreakpoint(process, address, accessKind, size, false);

            breakpoint.SetEnabled(true);

            lock (breakpointLock)
                dataBreakpoints.Add(address, breakpoint);

            return breakpoint;
        }

        internal void Remove(CordbBreakpoint breakpoint)
        {
            breakpoint.SetSuspended(true);

            //Can't remove the step breakpoint
            if (stepBreakpoint == breakpoint)
                return;

            lock (breakpointLock)
            {
                if (breakpoint is CordbNativeCodeBreakpoint n)
                    nativeCodeBreakpoints.Remove(n.Address);
                else if (breakpoint is CordbManagedCodeBreakpoint m)
                    managedCodeBreakpoints.Remove(m.CorDebugBreakpoint.Raw);
                else if (breakpoint is CordbDataBreakpoint d)
                    dataBreakpoints.Remove(d.Address);
                else
                    throw new NotImplementedException($"Don't know how to remove breakpoint of type '{breakpoint.GetType().Name}'.");
            }
        }

        internal CordbBreakpoint GetBreakpoint(CorDebugBreakpoint corDebugBreakpoint)
        {
            if (corDebugBreakpoint is CorDebugFunctionBreakpoint f)
            {
                lock (breakpointLock)
                {
                    if (managedCodeBreakpoints.TryGetValue(f.Raw, out var breakpoint))
                        return breakpoint;
                    else
                        throw new InvalidOperationException($"Couldn't find the breakpoint object that corresponds to managed breakpoint '{corDebugBreakpoint}'");
                }
            }
            else
                throw new NotImplementedException($"Don't know how to retrieve a the breakpoint object that corresponds to a breakpoint of type '{corDebugBreakpoint.GetType().Name}'");
        }

        internal bool TryGetBreakpoint(CORDB_ADDRESS address, out CordbNativeCodeBreakpoint breakpoint)
        {
            if (stepBreakpoint.Address == address)
            {
                breakpoint = stepBreakpoint;
                return true;
            }

            lock (breakpointLock)
            {
                if (nativeCodeBreakpoints.TryGetValue(address, out breakpoint))
                    return true;

                breakpoint = default;
                return false;
            }
        }

        public void ProcessDeferredBreakpoint()
        {
            if (deferredBreakpoint != null)
            {
                deferredBreakpoint.SetSuspended(false);
                deferredBreakpoint = null;
            }
        }

        public void RestoreCurrentBreakpoint()
        {
            if (currentBreakpoint == null)
                return;

            /* We can't just restore the last stored breakpoint. If we're currently at the address that this breakpoint is for,
             * we need to defer restoring it until we step to the next instruction. If that instruction doesn't already have
             * a breakpoint on it, we need to insert a sneaky step so that we can regain control and quickly resume the breakpoint
             * so that we're ready for the next time it is hit */

            var tid = process.Session.CallbackContext.UnmanagedEventThreadId;

            var ip = process.Threads[tid].RegisterContext.IP;
            
            if (currentBreakpoint.Address != ip)
            {
                //We're not currently at the instruction the breakpoint pertains to. Great! Just resume it now then
                currentBreakpoint.SetSuspended(false);
                return;
            }

            //We need to defer this breakpoint. Is there a breakpoint at the next instruction already, or do we need to insert a sneaky step?
            Debug.Assert(deferredBreakpoint == null);
            deferredBreakpoint = currentBreakpoint;

            throw new NotImplementedException();
        }

        public void AddNativeStep(int? threadId, bool stepOver)
        {
            threadId ??= process.Threads.ActiveThread.Id;

            var thread = process.Threads[threadId.Value];

            if (stepOver)
            {
                //They want us to step over, but if there isn't actually a call or anything, we can just do a single step instead

                var instr = process.ProcessDisassembler.Disassemble(thread.RegisterContext.IP).Instruction;

                if (instr.Mnemonic == Mnemonic.Call)
                {
                    //Need to step over it
                    stepBreakpoint.Address = instr.NextIP;
                    stepBreakpoint.SetSuspended(false);
                }
                else if (instr.Mnemonic == Mnemonic.Int3)
                {
                    //When we continue, we're going to see that there's a hard int3 and manually move the IP over it.
                    //If we were to single step here, we'd end up double stepping - once when we increment the IP, and again
                    //when we single step. As such, instead, we want to instead set a breakpoint on the next instruction, so that
                    //we then immediately trip over it after the IP is incremented past the current int3
                    stepBreakpoint.Address = instr.NextIP;
                    stepBreakpoint.SetSuspended(false);
                }
                else
                {
                    //Don't need to actually step over, do a single step instead
                    stepOver = false;
                }
            }

            //If we're doing a step in, or we don't actually need to step over, just do a single step
            if (!stepOver)
            {
                stepBreakpoint.Address = 0;
                thread.RegisterContext.EFlags |= X86_CONTEXT_FLAGS.TF;
                thread.TrySaveRegisterContext();
            }
        }

        public IEnumerator<CordbBreakpoint> GetEnumerator()
        {
            lock (breakpointLock)
            {
                return managedCodeBreakpoints.Values
                    .Cast<CordbBreakpoint>()
                    .Concat(nativeCodeBreakpoints.Values.Cast<CordbBreakpoint>())
                    .Concat(dataBreakpoints.Values.Cast<CordbBreakpoint>())
                    .ToList()
                    .GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
