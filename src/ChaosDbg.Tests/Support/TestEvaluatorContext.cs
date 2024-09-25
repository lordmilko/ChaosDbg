using System;
using ChaosDbg.Evaluator.Masm;
using Iced.Intel;

namespace ChaosDbg.Tests
{
    class TestEvaluatorContext : IMasmEvaluatorContext
    {
        public long GetRegisterValue(Register register)
        {
            switch (register)
            {
                case Register.AH:
                    return 3;

                case Register.ESI:
                    return 0x032c7c68;

                default:
                    throw new NotImplementedException($"Don't know what value to use for register '{register}'.");
            }
        }

        public bool TryGetPointerValue(long address, out long result)
        {
            switch (address)
            {
                case 0x77abdc60:
                    result = 0x8b55ff8b;
                    return true;

                default:
                    result = default;
                    return false;
            }
        }

        public long GetCurrentIP() => 0x77b18087;

        public bool TryGetModuleQualifiedSymbolValue(string moduleName, string symbolName, out long address)
        {
            switch (moduleName)
            {
                case "ntdll":
                    switch (symbolName)
                    {
                        case "LdrInitializeThunk":
                            address = 0x77abdc60;
                            return true;
                    }
                    break;

                case "foo":
                    switch (symbolName)
                    {
                        case "bar::baz":
                            address = 40;
                            return true;
                    }
                    break;
            }

            address = default;
            return false;
        }

        public bool TryGetSimpleSymbolValue(string symbolName, out long address)
        {
            switch (symbolName)
            {
                case "ntdll":
                    address = 0x77a60000;
                    return true;

                case "foo.bar":
                    address = 20;
                    return true;

                default:
                    address = default;
                    return false;
            }
        }
    }
}
