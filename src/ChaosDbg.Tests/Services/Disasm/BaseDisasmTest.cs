using System.IO;
using System.Runtime.Serialization;
using ChaosDbg.Disasm;
using ChaosLib;
using ChaosLib.Memory;
using PESpy;

namespace ChaosDbg.Tests
{
    public abstract class BaseDisasmTest : BaseTest
    {
        protected NativeDisassembler CreateDisassembler(long ip, bool is32Bit, params byte[] bytes)
        {
            var stream = new MemoryStream(bytes);

            var peFile = (PEFile) FormatterServices.GetUninitializedObject(typeof(PEFile));

            var ntHeaders = new ImageNtHeaders
            {
                OptionalHeader = new ImageOptionalHeader
                {
#pragma warning disable RS0030 //Fake physical stream
                    ImageBase = ip
#pragma warning restore RS0030
                }
            };

            var sectionHeaders = new[]
            {
                new ImageSectionHeader
                {
                    VirtualAddress = 1,
                    VirtualSize = 10
                }
            };

            ReflectionExtensions.SetFieldValue(peFile, "ntHeaders", ntHeaders);
            ReflectionExtensions.SetFieldValue(peFile, "sectionHeaders", sectionHeaders);

            var setRegionFlagMethod = typeof(PEFile).GetMethodInfo("SetRegionFlag");
            setRegionFlagMethod.Invoke(peFile, new object[] {PERegionKind.NtHeaders | PERegionKind.SectionHeaders});

            var rvaStream = new PERvaToPhysicalStream(stream, peFile);
            rvaStream.Seek(1, SeekOrigin.Begin); //Querying for an RVA of 0 is disallowed, so we need everything to start from 1

            var engine = NativeDisassembler.FromStream(rvaStream, is32Bit);

            return engine;
        }
    }
}
