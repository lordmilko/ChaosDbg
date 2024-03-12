using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a breakpoint that was set in managed code.
    /// </summary>
    public class CordbManagedCodeBreakpoint : CordbBreakpoint
    {
        public CorDebugFunctionBreakpoint CorDebugBreakpoint { get; }

        public CordbManagedCodeBreakpoint(CorDebugFunctionBreakpoint corDebugBreakpoint, bool isOneShot) : base(isOneShot)
        {
            CorDebugBreakpoint = corDebugBreakpoint;

            //Managed breakpoints are already enabled by default
            IsEnabled = true;
        }

        protected override void Activate(bool activate)
        {
            CorDebugBreakpoint.Activate(activate);
        }
    }
}
