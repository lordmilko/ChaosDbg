using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using ChaosDbg.TTD;
using ClrDebug;
using ClrDebug.TTD;

namespace ChaosDbg.PowerShell.Cmdlets
{
    [Alias("Get-TtdCallTree")]
    [Cmdlet(VerbsCommon.Get, "TtdFunctionCallTree")]
    internal class GetTtdFunctionCallTree : DbgEngCmdlet //Internal because it doesn't currently work
    {
        [Parameter(Mandatory = true, Position = 0)]
        public string[] FromFunction { get; set; }

        [Parameter(Mandatory = false, Position = 1)]
        public string[] ToFunction { get; set; }

        protected override unsafe void ProcessRecord()
        {
            var engine = ActiveEngine;

            var replayEngine = engine.TTD.ReplayEngine;

            //First, get every time the specified function was called
            var calls = engine.TTD.GetFunctionCalls(FromFunction);

            if (calls.Length == 0)
                return;

            var cursor = replayEngine.NewCursor();

            int targetThreadId = 0;

            //TTD will iterate over all calls and returns between the specified start and end range. We're only interested in events
            //that occurred on the thread the given call we're tracing occurred on

            ConcurrentBag<TtdCallReturnEvent> callReturnBag = new ConcurrentBag<TtdCallReturnEvent>();

            //TTD will blast events at us from a thread pool. Thus, we must ensure that any external structures we touch are thread-safe
            cursor.SetCallReturnCallback((context, functionAddress, returnAddress, threadView) =>
            {
                if (threadView.ThreadInfo.UniqueThreadId.Value != targetThreadId)
                    return;

                callReturnBag.Add(new TtdCallReturnEvent(functionAddress, returnAddress, threadView.ThreadInfo, threadView.Position));
            });

            ConcurrentBag<TtdIndirectJumpEvent> indirectJumpBag = new ConcurrentBag<TtdIndirectJumpEvent>();

            cursor.SetIndirectJumpCallback((context, address, threadView) =>
            {
                if (threadView.ThreadInfo.UniqueThreadId.Value != targetThreadId)
                    return;

                indirectJumpBag.Add(new TtdIndirectJumpEvent(address, threadView.ThreadInfo, threadView.Position));
            });

            var knownSymbols = new Dictionary<long, string>();

            //Now, for each occurrence, navigate to when the function was called and trace all calls within it until it returns
            foreach (var call in calls)
            {
                var startPos = call.StartPosition;
                var endPos = call.EndPosition;

                cursor.SetPosition(startPos);
                targetThreadId = call.UniqueThreadId.Value;

                var replayResult = cursor.ReplayForward(endPos, StepCount.Max);

                string GetSymbolName(GuestAddress address)
                {
                    if (knownSymbols.TryGetValue(address, out var existing))
                        return existing;
                    else
                    {
                        string name;

                        if (engine.ActiveClient.Symbols.TryGetNameByOffset(address, out var result) == HRESULT.S_OK)
                        {
                            name = result.Displacement == 0 ? result.NameBuffer : result.NameBuffer + "+0x" + result.Displacement.ToString("X");
                        }
                        else
                            name = address.ToString();

                        knownSymbols[address] = name;

                        return name;
                    }
                }

                foreach (var item in callReturnBag)
                    item.Name = GetSymbolName(item.FunctionAddress);

                foreach (var item in indirectJumpBag)
                    item.Name = GetSymbolName(item.Address);

                var threadItems = callReturnBag.Cast<TtdCallTreeEvent>().Concat(indirectJumpBag).OrderBy(v => v.Position).ToArray();

                var threadStack = new ThreadStack(default);

                if (callReturnBag.Count == 0)
                {
                    WriteWarning("No matching events were found");
                    return;
                }

                foreach (var item in threadItems)
                {
                    if (item is TtdCallReturnEvent c)
                    {
                        if (c.ReturnAddress == default)
                            threadStack.Leave(c);
                        else
                            threadStack.Enter(c);
                    }
                    else
                    {
                        threadStack.AddIndirect((TtdIndirectJumpEvent) item);
                    }
                }

                var root = threadStack.Root;

                root.Function = call;

                WriteObject(root);
            }
        }
    }
}
