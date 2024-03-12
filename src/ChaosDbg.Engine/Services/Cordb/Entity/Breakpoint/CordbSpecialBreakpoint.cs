using System;

namespace ChaosDbg.Cordb
{
    public enum CordbSpecialBreakpointKind
    {
        Loader
    }

    /// <summary>
    /// Represents a breakpoint that pertains to a special process event (such as the loader breakpoint)
    /// </summary>
    public class CordbSpecialBreakpoint : CordbBreakpoint
    {
        public CordbSpecialBreakpointKind Kind { get; }

        public CordbSpecialBreakpoint(CordbSpecialBreakpointKind kind) : base(false)
        {
            Kind = kind;
        }

        protected override void Activate(bool activate)
        {
            throw new NotSupportedException("Special breakpoints cannot be controlled");
        }

        public override string ToString()
        {
            return Kind.ToString();
        }
    }
}
