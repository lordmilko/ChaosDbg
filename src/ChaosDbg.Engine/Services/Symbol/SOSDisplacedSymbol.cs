﻿using ChaosLib.Symbols;

namespace ChaosDbg.Symbol
{
    public class SOSDisplacedSymbol : SOSSymbol, IDisplacedSymbol
    {
        public long Displacement { get; }

        public ISymbol Symbol { get; }

        public SOSDisplacedSymbol(long displacement, IManagedSymbol symbol) : base(symbol.Name, symbol.Address + displacement, symbol.Module)
        {
            Displacement = displacement;
            Symbol = symbol;
        }

        public override string ToString()
        {
            if (Displacement == 0)
                return base.ToString();

            return $"{base.ToString()}+{Displacement:X}";
        }
    }
}
