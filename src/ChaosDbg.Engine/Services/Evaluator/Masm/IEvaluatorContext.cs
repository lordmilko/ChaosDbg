using Iced.Intel;

namespace ChaosDbg.Evaluator.Masm
{
    public interface IEvaluatorContext
    {
        long GetRegisterValue(Register register);

        bool TryGetPointerValue(long address, out long result);

        long GetCurrentIP();

        bool TryGetModuleQualifiedSymbolValue(string moduleName, string symbolName, out long address);

        bool TryGetSimpleSymbolValue(string symbolName, out long address);
    }
}
