using Iced.Intel;

namespace ChaosDbg.Evaluator.Masm
{
    public interface IEvaluatorContext
    {
        void BeginEvaluation();

        void EndEvaluation();

        long GetRegisterValue(Register register);

        bool TryGetPointerValue(long address, out long result);

        long GetCurrentIP();

        bool TryGetSymbolValue(string symbolName, out long address);
    }
}
