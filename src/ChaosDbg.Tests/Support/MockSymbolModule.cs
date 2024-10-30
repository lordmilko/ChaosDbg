using System;
using System.Collections.Generic;
using System.Linq;
using ClrDebug;
using ClrDebug.DIA;
using SymHelp.Symbols;

namespace ChaosDbg.Tests
{
    class MockSymbolModule : IUnmanagedSymbolModule
    {
        private IUnmanagedSymbolModule symbolModule;

        public string Name => symbolModule.Name;
        public string ModulePath => symbolModule.ModulePath;
        public long Address => symbolModule.Address;
        public int Length => symbolModule.Length;

        private string[] allowed;

        public MockSymbolModule(IUnmanagedSymbolModule symbolModule, params string[] allowed)
        {
            this.symbolModule = symbolModule;
            this.allowed = allowed;
        }

        IEnumerable<ISymbol> ISymbolModule.EnumerateSymbols() => EnumerateSymbols();

        public IEnumerable<IManagedSymbol> EnumerateManagedSymbols()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IManagedVariableSymbol> EnumerateManagedVariables(mdMethodDef methodDef, int ilOffset)
        {
            throw new NotImplementedException();
        }

        public string[] GetSourceLinkJson()
        {
            throw new NotImplementedException();
        }

        public DiaSession DiaSession { get; }

        public IEnumerable<IUnmanagedSymbol> EnumerateSymbols()
        {
            foreach (var item in symbolModule.EnumerateSymbols())
            {
                if (IsSymbolAllowed(item))
                    yield return item;
            }
        }

        public IDisplacedSymbol GetSymbolFromAddress(long address)
        {
            var result = symbolModule.GetSymbolFromAddress(address);

            if (result == null)
                return null;

            if (IsSymbolAllowed(result))
                return result;

            return null;
        }

        public IDisplacedSymbol GetInlineSymbolFromAddress(long address, int inlineFrameContext)
        {
            throw new NotImplementedException();
        }

        public ISymbol GetSymbolFromName(string name, SymbolKind kind)
        {
            throw new NotImplementedException();
        }

        private bool IsSymbolAllowed(ISymbol symbol)
        {
            if (allowed.Any(a =>
            {
                if (a == symbol.Name)
                    return true;

                //DbgHelp trims one of the underscores
                if (a.StartsWith("_"))
                {
                    if (a == "_" + symbol.Name)
                        return true;
                }

                return false;
            }))
                return true;

            return false;
        }
    }
}
