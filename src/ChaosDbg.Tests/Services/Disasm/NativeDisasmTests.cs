using System;
using System.IO;
using System.Text.RegularExpressions;
using ChaosDbg.DbgEng;
using ChaosDbg.Disasm;
using ChaosLib.Memory;
using ClrDebug.DbgEng;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PESpy;

namespace ChaosDbg.Tests
{
    [TestClass]
    public class NativeDisasmTests : BaseDisasmTest
    {
        [TestMethod]
        public void NativeDisasm_CompareDisasm()
        {
            WithProcess(client =>
            {
                var baseAddress = client.Symbols.GetModuleByIndex(0);

                var dbgEngMemoryStream = new DbgEngMemoryStream(client);
                dbgEngMemoryStream.Seek(baseAddress, SeekOrigin.Begin);

                var relativeDbgEngMemoryStream = new RelativeToAbsoluteStream(dbgEngMemoryStream, baseAddress);

                var peFile = PEFile.FromStream(relativeDbgEngMemoryStream, true);
                relativeDbgEngMemoryStream.Seek(peFile.OptionalHeader.AddressOfEntryPoint, SeekOrigin.Begin);

                var dis = NativeDisassembler.FromStream(dbgEngMemoryStream, peFile.OptionalHeader.Magic == PEMagic.PE32, new DbgEngDisasmSymbolResolver(client));

                CompareDisassembly(client, dis, baseAddress + peFile.OptionalHeader.AddressOfEntryPoint);
            });
        }

        private void CompareDisassembly(DebugClient client, NativeDisassembler dis, long startIP)
        {
            string DisDbgEng()
            {
                var result = client.Control.Disassemble(startIP, DEBUG_DISASM.EFFECTIVE_ADDRESS);
                startIP = result.EndOffset;
                var value = result.Buffer.TrimEnd('\n');

                value = Regex.Replace(value, "(.+) (\\[br|ds:|ss:).+", "$1").TrimEnd(' ');

                return value;
            }

            string DisIced()
            {
                var result = dis.Disassemble();

                Assert.IsNotNull(result);

                return dis.Format(result);
            }

            for (var i = 0; i < 10; i++)
            {
                var first = DisDbgEng();
                var second = DisIced();

                Assert.AreEqual(first, second);
            }
        }

        private void WithProcess(Action<DebugClient> action)
        {
            var engineProvider = GetService<DebugEngineProvider>();

            //Protect g_Machine from other threads
            engineProvider.WithDbgEng(services =>
            {
#pragma warning disable CS0618
                using var client = services.SafeDebugCreate(false);
#pragma warning restore CS0618

                try
                {
                    client.Control.EngineOptions = DEBUG_ENGOPT.INITIAL_BREAK | DEBUG_ENGOPT.FINAL_BREAK;
                    client.CreateProcessAndAttach(0, "notepad", DEBUG_CREATE_PROCESS.CREATE_NEW_CONSOLE | DEBUG_CREATE_PROCESS.DEBUG_ONLY_THIS_PROCESS, 0, DEBUG_ATTACH.DEFAULT);

                    client.Control.WaitForEvent(DEBUG_WAIT.DEFAULT, -1);

                    action(client);
                }
                finally
                {
                    client.TerminateCurrentProcess();

                    client.TryEndSession(DEBUG_END.ACTIVE_TERMINATE);
                }
            });
        }

        //We need to test reading a file with no entrypoint. Until we encounter such a file, this scenario is unsupported
    }
}
