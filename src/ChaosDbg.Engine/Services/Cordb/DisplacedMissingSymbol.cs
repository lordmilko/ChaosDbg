using System;
using ChaosLib.Symbols;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a symbol that is a simple offset into a module.
    /// </summary>
    class DisplacedMissingSymbol : IDisplacedSymbol
    {
        public string Name { get; }
        
        public long Address { get; }
        
        public long Displacement { get; }

        public ISymbolModule Module { get; }

        public ISymbol Symbol { get; }

        internal DisplacedMissingSymbol(long displacement, string name, long address)
        {
            //The address contains the displacement in it already, so we dont need to adjust it
            Displacement = displacement;
            Name = name;
            Address = address;

            Symbol = new MissingSymbol(Name, Address - displacement);
        }

        class MissingSymbol : ISymbol
        {
            public string Name { get; }
            public long Address { get; }
            public ISymbolModule Module => throw new NotSupportedException();

            internal MissingSymbol(string name, long address)
            {
                Name = name;
                Address = address;
            }

            public override string ToString()
            {
                return Name;
            }
        }
    }
}
