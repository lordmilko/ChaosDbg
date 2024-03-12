using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents an mscordbi-aware breakpoint that was set in native code via <see cref="CorDebugProcess.SetUnmanagedBreakpoint(CORDB_ADDRESS, int)"/>
    /// </summary>
    public class CordbNativeCodeBreakpoint : CordbBreakpoint
    {
        /// <summary>
        /// Gets the display name of this breakpoint.
        /// </summary>
        public string Name { get; }

        public CORDB_ADDRESS Address { get; protected set; }

        protected CordbProcess process;

        public CordbNativeCodeBreakpoint(string name, CORDB_ADDRESS address, CordbProcess process, bool isOneShot) : base(isOneShot)
        {
            Name = name;
            Address = address;
            this.process = process;
        }

        protected override void Activate(bool activate)
        {
            //When an unmanaged breakpoint is set, the mscordbi only replaces a single byte with 0xcc. On ARM64 it needs to replace 4 bytes.
            //See CordbProcess::SetUnmanagedBreakpointInternal(). No need to store the byte that is overwritten; mscordbi stores this for us

            if (activate)
                process.CorDebugProcess.SetUnmanagedBreakpoint(Address, 1);
            else
                process.CorDebugProcess.ClearUnmanagedBreakpoint(Address);
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
