namespace ChaosDbg.Cordb
{
    public class CordbModuleLoadBreakpoint : CordbBreakpoint
    {
        protected override void Activate(bool activate)
        {
            throw new System.NotImplementedException();
        }

        public CordbModuleLoadBreakpoint(bool isOneShot) : base(isOneShot)
        {
        }
    }
}
