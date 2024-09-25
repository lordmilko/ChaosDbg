using ChaosLib.Symbols;

namespace ChaosDbg.Symbols
{
    public class SOSJitHelperSymbol : SOSSymbol
    {
        /// <summary>
        /// Gets the <see cref="IUnmanagedSymbol"/> that is associated with this JIT helper.
        /// </summary>
        public IUnmanagedSymbol UnmanagedSymbol { get; }

        public SOSJitHelperSymbol(string name, long address, IUnmanagedSymbol unmanagedSymbol, ISymbolModule module) : base(name, address, module)
        {
            UnmanagedSymbol = unmanagedSymbol;
        }

        public override string ToString()
        {
            if (UnmanagedSymbol != null)
                return $"{Name} ({UnmanagedSymbol})";

            return base.ToString();
        }
    }
}
