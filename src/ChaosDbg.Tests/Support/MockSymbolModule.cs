﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ChaosLib.Metadata;
using ClrDebug.DIA;

namespace ChaosDbg.Tests
{
    class MockSymbolModule : IUnmanagedSymbolModule
    {
        private IUnmanagedSymbolModule symbolModule;

        public string Name => symbolModule.Name;
        public string FilePath => symbolModule.FilePath;
        public long Address => symbolModule.Address;
        public long Length => symbolModule.Length;

        private string[] allowed;

        public MockSymbolModule(IUnmanagedSymbolModule symbolModule, params string[] allowed)
        {
            this.symbolModule = symbolModule;
            this.allowed = allowed;
        }

        IEnumerable<ISymbol> ISymbolModule.EnumerateSymbols() => EnumerateSymbols();

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
