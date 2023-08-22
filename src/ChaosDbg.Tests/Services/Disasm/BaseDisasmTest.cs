using System.IO;
using ChaosDbg.Disasm;
using ChaosDbg.Metadata;

namespace ChaosDbg.Tests
{
    public abstract class BaseDisasmTest
    {
        protected INativeDisassembler CreateDisassembler(long ip, params byte[] bytes)
        {
            var stream = new MemoryStream(bytes);

            var peFile = new MockPEFile
            {
                OptionalHeader = new MockImageOptionalHeader
                {
                    ImageBase = (ulong) ip
                }
            };

            var rvaStream = new PERvaToPhysicalStream(stream, peFile);
            rvaStream.Seek(0, SeekOrigin.Begin);

            var engine = new NativeStreamDisassembler(rvaStream, true);

            return engine;
        }
    }
}
