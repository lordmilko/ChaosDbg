using ClrDebug;
using SymHelp.Symbols;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents an mscordbi-unaware breakpoint that was directly patched into the target process,
    /// allowing things to be broken into that would not normally be supported, such as the CLR itself.
    /// </summary>
    public class CordbRawCodeBreakpoint : CordbNativeCodeBreakpoint
    {
        private byte originalByte;
        protected bool hasOriginalByte;

        public CordbRawCodeBreakpoint(IDisplacedSymbol symbol, CORDB_ADDRESS address, CordbProcess process, bool isOneShot) : base(symbol, address, process, isOneShot)
        {
        }

        protected override void Activate(bool activate)
        {
            if (activate)
            {
                if (!hasOriginalByte)
                {
                    originalByte = process.DataTarget.ReadVirtual<byte>(Address);
                    hasOriginalByte = true;
                }

                //CordbProcess::WriteMemory has a knob INTERNAL_DbgCheckInt3 that can be used
                //to assert that you're not trying to bypass CordbProcess::SetUnmanagedBreakpoint.
                //This only applies in debug builds of mscordbi, and since it also requires a knob,
                //we're not concerned that this will break our ability to use ChaosDbg with debug builds
                process.DataTarget.WriteVirtual<byte>(Address, 0xcc);
            }
            else
            {
                //If we haven't even set the breakpoint to begin with, nothing to do
                if (!hasOriginalByte)
                    return;

                process.DataTarget.WriteVirtual(Address, originalByte);
            }
        }
    }
}
