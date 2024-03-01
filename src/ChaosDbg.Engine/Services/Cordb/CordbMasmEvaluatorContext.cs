using System;
using ChaosDbg.Evaluator.Masm;
using ClrDebug;
using Iced.Intel;

namespace ChaosDbg.Cordb
{
    public class CordbMasmEvaluatorContext : IEvaluatorContext
    {
        private CordbEngineProvider engineProvider;

        [ThreadStatic]
        private CrossPlatformContext context;

        public CordbMasmEvaluatorContext(CordbEngineProvider engineProvider)
        {
            this.engineProvider = engineProvider;
        }

        private CordbEngine engine => engineProvider.ActiveEngine;

        public void BeginEvaluation()
        {
        }

        public void EndEvaluation()
        {
            context = null;
        }

        public long GetRegisterValue(Register register)
        {
            context = null;

            if (context == null)
            {
                var threadId = engine.Process.Threads.ActiveThread.Id;
                var flags = engine.Process.Is32Bit ? ContextFlags.X86ContextAll : ContextFlags.AMD64ContextAll;
                var raw = ((ICLRDataTarget) engine.Process.DAC.DataTarget).GetThreadContext<CROSS_PLATFORM_CONTEXT>(threadId, flags);
                context = new CrossPlatformContext(flags, raw);
            }

            if (engine.Process.Is32Bit)
                return context.Raw.X86Context.GetRegisterValue(register);
            else
                return context.Raw.Amd64Context.GetRegisterValue(register);
        }

        public bool TryGetPointerValue(long address, out long result)
        {
            ICLRDataTarget dataTarget = engine.Process.DAC.DataTarget;

            if (engine.Process.Is32Bit)
            {
                if (dataTarget.TryReadVirtual<uint>(address, out var value) == HRESULT.S_OK)
                {
                    result = value;
                    return true;
                }
            }
            else
            {
                if (dataTarget.TryReadVirtual<long>(address, out result) == HRESULT.S_OK)
                    return true;
            }

            result = default;
            return false;
        }

        public long GetCurrentIP() => GetRegisterValue(engine.Process.Is32Bit ? Register.EIP : Register.RIP);

        public bool TryGetSymbolValue(string symbolName, out long address)
        {
            if (engine.Process.DbgHelp.TrySymFromName(symbolName, out var symbol) == HRESULT.S_OK)
            {
                address = symbol.Address;
                return true;
            }

            address = default;
            return false;
        }
    }
}
