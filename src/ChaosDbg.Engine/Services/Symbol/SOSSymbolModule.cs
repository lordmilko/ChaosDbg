using System;
using System.Collections.Generic;
using System.IO;
using ChaosLib.Metadata;
using ClrDebug;

namespace ChaosDbg.Symbol
{
    public class SOSSymbolModule : IManagedSymbolModule
    {
        private object objLock = new object();

        //Unlike with native modules, we only know about symbols whose addresses have directly been queried and cached here before
        private Dictionary<long, IManagedSymbol> knownSymbols = new Dictionary<long, IManagedSymbol>();

        long ISymbolModule.Address => Address;

        public CLRDATA_ADDRESS Address { get; }

        public string Name { get; }

        public string FilePath { get; }

        public long Length => throw new NotImplementedException();

        public SOSSymbolModule(CLRDATA_ADDRESS address, string name)
        {
            Address = address;
            Name = Path.GetFileNameWithoutExtension(name);

            if (Path.IsPathRooted(name))
                FilePath = name;
        }

        public void AddSymbol(IManagedSymbol symbol)
        {
            lock (objLock)
                knownSymbols.Add(symbol.Address, symbol);
        }

        IEnumerable<ISymbol> ISymbolModule.EnumerateSymbols() => EnumerateSymbols();

        public IEnumerable<IManagedSymbol> EnumerateSymbols()
        {
            throw new NotImplementedException();
        }

        public IDisplacedSymbol GetSymbolFromAddress(long address)
        {
            throw new NotImplementedException();
        }

        //Not supported
        public IDisplacedSymbol GetInlineSymbolFromAddress(long address, int inlineFrameContext) => null;

        public override string ToString()
        {
            return Name;
        }
    }
}
