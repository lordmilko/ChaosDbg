using ChaosLib.Symbols;

namespace ChaosDbg.Symbol
{
    /// <summary>
    /// Represents a symbol pertaining to managed code or the CLR that was retrieved from SOS.
    /// </summary>
    public class SOSSymbol : IManagedSymbol
    {
        public string Name { get; }
        public long Address { get; }
        public ISymbolModule Module { get; }

        public SOSSymbol(string name, long address, ISymbolModule module)
        {
            Name = name;
            Address = address;
            Module = module;
        }

        public override string ToString()
        {
            return $"{Module?.ToString() ?? "<Global>"}!{Name}";
        }
    }
}
