using ChaosDbg.Disasm;
using ChaosLib;
using ClrDebug;
using ClrDebug.DIA;
using SymHelp.Symbols.MicrosoftPdb;
using SymHelp.Symbols.MicrosoftPdb.TypedData;

namespace ChaosDbg.Cordb
{
    class CordbTypedDataAccessor : ITypedDataAccessor
    {
        private CordbNativeFrame frame;

        public CordbTypedDataAccessor(CordbNativeFrame frame)
        {
            this.frame = frame;
        }

        public bool TryReadMemory<T>(long address, out T value) where T : struct =>
            frame.Thread.Process.DataTarget.TryReadVirtual<T>(address, out value) == HRESULT.S_OK;

        public bool TryReadPointer(long address, out long value) =>
            ((IMemoryReader) frame.Thread.Process.DataTarget).TryReadPointer(address, out value) == HRESULT.S_OK;

        public object GetRegister(CV_HREG_e register) =>
            register.ToIcedRegister(frame.Thread.Process.MachineType);

        public long GetRegisterValue(CV_HREG_e register)
        {
            var icedRegister = register.ToIcedRegister(frame.Thread.Process.MachineType);

            var value = frame.Context.GetRegisterValue(icedRegister);

            return value;
        }

        public MicrosoftPdbSymbol GetSymbolForName(string name) =>
            (MicrosoftPdbSymbol) frame.Thread.Process.Symbols.GetSymbolFromName(name);
    }
}
