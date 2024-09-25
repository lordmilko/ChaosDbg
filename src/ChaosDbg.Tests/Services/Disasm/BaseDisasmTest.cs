using System.IO;
using ChaosDbg.Disasm;
using ChaosLib.PortableExecutable;

namespace ChaosDbg.Tests
{
    public abstract class BaseDisasmTest : BaseTest
    {
        protected INativeDisassembler CreateDisassembler(long ip, bool is32Bit, params byte[] bytes)
        {
            var stream = new MemoryStream(bytes);

            var peFile = new PEFile
            {
                NtHeaders = new ImageNtHeaders
                {
                    OptionalHeader = new ImageOptionalHeader
                    {
#pragma warning disable RS0030 //Fake physical stream
                        ImageBase = ip
#pragma warning restore RS0030
                    }
                },
                SectionHeaders = new[]
                {
                    new ImageSectionHeader
                    {
                        VirtualAddress = 0,
                        VirtualSize = 10
                    }
                }
            };

            var rvaStream = new PERvaToPhysicalStream(stream, peFile);
            rvaStream.Seek(0, SeekOrigin.Begin);

            var engine = new NativeDisassembler(rvaStream, is32Bit);

            return engine;
        }
    }
}
