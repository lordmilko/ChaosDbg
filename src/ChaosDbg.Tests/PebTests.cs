using System;
using System.Diagnostics;
using ChaosLib;
using ChaosLib.Symbols;
using ChaosLib.Symbols.MicrosoftPdb.TypedData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SymHelp.Symbols;
using SymHelp.Symbols.MicrosoftPdb.TypedData;
using TestApp;

namespace ChaosDbg.Tests
{
    [MTATestClass]
    public unsafe class PebTests : BaseTest
    {
        [TestMethod]
        public void Peb_Modules_x86() =>
            TestPebModules(TestType.CordbEngine_Thread_StackTrace_InternalFrames, true);

        [TestMethod]
        public void Peb_Modules_x64() =>
            TestPebModules(TestType.CordbEngine_Thread_StackTrace_InternalFrames, false);

        private void TestPebModules(
            TestType testType,
            bool is32Bit)
        {
            if (IntPtr.Size == 4 && !is32Bit)
                Assert.Inconclusive("This test can only run in a 32-bit test process");

            TestCreate(
                testType,
                is32Bit,
                process =>
                {
                    var reader = new LiveProcessMemoryReader(process.Handle);
                    var remotePeb = new RemotePeb(process);

                    var symbolProvider = new LiveSymbolProvider(GetService<INativeLibraryProvider>(), GetService<ISymSrv>(), reader);
                    symbolProvider.DiscoverModules(process.Handle);

                    var typedValueAccessor = new LiveProcessTypedDataAccessor(process.Handle, symbolProvider);

                    //Ensure we use Peb32 for the purposes of this test, which RemotePeb will resolve
                    var typedPeb = symbolProvider.CreateObjectTypedValue(remotePeb.Address, "ntdll!_PEB", typedValueAccessor);

                    var typedLdr = (PointerTypedValue) typedPeb["Ldr"];
                    var remoteLdr = remotePeb.Ldr;

                    var typedInLoadOrder = typedLdr["InLoadOrderModuleList"].AsType("ntdll!_LDR_DATA_TABLE_ENTRY")["InLoadOrderLinks"].ToArray();
                    var remoteInLoadOrder = remoteLdr.InLoadOrderModuleList;

                    var typedInMemoryOrder = typedLdr["InMemoryOrderModuleList"].AsType("ntdll!_LDR_DATA_TABLE_ENTRY")["InMemoryOrderLinks"].ToArray();
                    var remoteInMemoryOrder = remoteLdr.InMemoryOrderModuleList;

                    var typedInInitializationOrder = typedLdr["InInitializationOrderModuleList"].AsType("ntdll!_LDR_DATA_TABLE_ENTRY")["InInitializationOrderLinks"].ToArray();
                    var remoteInInitializationOrder = remoteLdr.InInitializationOrderModuleList;

                    var sets = new[]
                    {
                        Tuple.Create(typedInLoadOrder, remoteInLoadOrder),
                        Tuple.Create(typedInMemoryOrder, remoteInMemoryOrder),
                        Tuple.Create(typedInInitializationOrder, remoteInInitializationOrder)
                    };

                    foreach (var set in sets)
                    {
                        var typedItems = set.Item1;
                        var remoteItems = set.Item2;

                        Assert.AreEqual(typedItems.Length, remoteItems.Length);

                        for (var i = 0; i < typedItems.Length; i++)
                        {
                            var typedItem = typedItems[i];
                            var remoteItem = remoteItems[i];

                            Assert.AreEqual(typedItem["FullDllName"]["Buffer"].GetPrimitiveValue(), remoteItem.FullDllName);
                            Assert.AreEqual(typedItem["BaseDllName"]["Buffer"].GetPrimitiveValue(), remoteItem.BaseDllName);
                        }
                    }
                }
            );
        }
    }
}
