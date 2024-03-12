using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents the special breakpoint that is used for stepping.
    /// </summary>
    public class CordbStepBreakpoint : CordbRawCodeBreakpoint
    {
        public new CORDB_ADDRESS Address
        {
            get => base.Address;
            set => base.Address = value;
        }

        public CordbStepBreakpoint(CordbProcess process) : base(null, 0, process, true)
        {
            //The step breakpoint should always be "enabled", and will get suspended/resumed as required
            IsEnabled = true;
            IsSuspended = true;
        }

        protected override void Activate(bool activate)
        {
            //Any time we reactivate this singleton, we need to re-request the new original byte
            if (activate)
                hasOriginalByte = false;

            base.Activate(activate);
        }
    }
}
