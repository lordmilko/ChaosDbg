using System.IO;
using ChaosDbg.Cordb;
using ClrDebug;

namespace ChaosDbg.IL
{
    /// <summary>
    /// Provides facilities for creating <see cref="ILDisassembler"/> instances
    /// from different input sources.
    /// </summary>
    public class ILDisassemblerProvider
    {
        public ILDisassembler CreateDisassembler(CorDebugFunction corDebugFunction, CordbManagedModule module)
        {
            var code = corDebugFunction.ILCode;

            /* MSDN says GetCode() is deprecated, and to use ICorDebugCode2::GetCodeChunks instead.
             * There are two types of objects that can underpin ICorDebugCode:
             * - CordbCode, which only implements ICorDebugCode, and
             * - CordbNativeCode, which implements all ICorDebugCode interfaces
             *
             * Therefore, you can't necessarily use GetCodeChunks if you have non-native code */

            var size = code.Size;
            var bytes = code.GetCode(0, size, size);

            var stream = new MemoryStream(bytes);

            return new ILDisassembler(stream, module.MetaDataProvider);
        }
    }
}
