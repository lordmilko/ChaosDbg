using System;
using ChaosDbg.Evaluator.Masm;
using ClrDebug.TTD;
using Iced.Intel;

namespace ChaosDbg.TTD
{
    /// <summary>
    /// Provides facilities for evaluating <see cref="MasmEvaluator"/> expression values in the context of a TTD <see cref="Cursor"/>.
    /// </summary>
    class TtdMasmEvaluatorContext : IMasmEvaluatorContext
    {
        private Cursor cursor;
        private CrossPlatformContext context;

        public TtdMasmEvaluatorContext(Cursor cursor, CrossPlatformContext context)
        {
            this.cursor = cursor;
            this.context = context;
        }

        public long GetRegisterValue(Register register)
        {
            return context.GetRegisterValue(register);
        }

        public unsafe bool TryGetPointerValue(long address, out long result)
        {
            if (cursor.TryQueryMemoryBuffer<IntPtr>(address, QueryMemoryPolicy.Default, out var raw))
            {
                result = (long) (void*) raw;
                return true;
            }

            result = default;
            return false;
        }

        public long GetCurrentIP() =>
            context.IP;

        public bool TryGetModuleQualifiedSymbolValue(string moduleName, string symbolName, out long address)
        {
            throw new NotImplementedException();
        }

        public bool TryGetSimpleSymbolValue(string symbolName, out long address)
        {
            throw new NotImplementedException();
        }
    }
}
