using System;

namespace ChaosDbg
{
    public class EngineBreakpointHitEventArgs : EventArgs
    {
        public IDbgBreakpoint Breakpoint { get; }

        public EngineBreakpointHitEventArgs(IDbgBreakpoint breakpoint)
        {
            Breakpoint = breakpoint;
        }
    }
}
