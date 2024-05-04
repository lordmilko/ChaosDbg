using System.Collections.Generic;
using System.IO;
using ChaosDbg.Evaluator.Masm;
using ChaosLib.Symbols;
using ClrDebug;
using Iced.Intel;

namespace ChaosDbg.Cordb
{
    public class CordbMasmEvaluatorContext : IEvaluatorContext
    {
        private CordbProcess process;

        public CordbMasmEvaluatorContext(CordbProcess process)
        {
            this.process = process;
        }

        public long GetRegisterValue(Register register)
        {
            var context = process.Threads.ActiveThread.RegisterContext;

            var value = context.GetRegisterValue(register);

            return value;
        }

        public bool TryGetPointerValue(long address, out long result)
        {
            ICLRDataTarget dataTarget = process.DataTarget;

            if (process.Is32Bit)
            {
                //Doesn't need to be a managed read
                if (dataTarget.TryReadVirtual<uint>(address, out var value) == HRESULT.S_OK)
                {
                    result = value;
                    return true;
                }
            }
            else
            {
                //Doesn't need to be a managed read
                if (dataTarget.TryReadVirtual<long>(address, out result) == HRESULT.S_OK)
                    return true;
            }

            result = default;
            return false;
        }

        public long GetCurrentIP() => GetRegisterValue(process.Is32Bit ? Register.EIP : Register.RIP);
        public bool TryGetModuleQualifiedSymbolValue(string moduleName, string symbolName, out long address)
        {
            address = default;

            //Try with what the user asked for first

            var matches = process.Symbols.NativeSymEnumSymbols($"{moduleName}!{symbolName}");

            if (matches.Length == 0)
            {
                //If there were no matches, try again, stripping off the module file extension (if necessary)
                var ext = Path.GetExtension(moduleName).ToLower();

                switch (ext)
                {
                    case ".exe":
                    case ".dll":
                    case ".sys":
                        if (moduleName.Length > 4)
                        {
                            moduleName = moduleName.Substring(0, moduleName.Length - 4);

                            matches = process.Symbols.NativeSymEnumSymbols($"{moduleName}!{symbolName}");
                        }

                        break;
                }
            }

            if (matches.Length == 0)
                return false;

            if (matches.Length > 1)
                throw new InvalidExpressionException($"Expression '{moduleName}!{symbolName}' matched multiple symbols: {string.Join(", ", (IEnumerable<IUnmanagedSymbol>) matches)}");

            if (matches.Length == 1)
            {
                address = matches[0].Address;
                return true;
            }

            return false;
        }

        public bool TryGetSimpleSymbolValue(string symbolName, out long address)
        {
            if (process.Symbols.TryNativeSymFromName(symbolName, out var symbol))
            {
                address = symbol.Address;
                return true;
            }

            address = default;
            return false;
        }
    }
}
