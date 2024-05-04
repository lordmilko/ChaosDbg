using System;
using ChaosLib;
using ChaosLib.TypedData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
                    //Ensure we're not using the system32 version
                    GetService<NativeLibraryProvider>().GetModuleHandle(WellKnownNativeLibrary.DbgHelp);

                    using var dbgHelp = DbgHelpProvider.Acquire(process.Handle, invadeProcess: true);

                    var reader = new MemoryReader(process.Handle);
                    var remotePeb = new RemotePeb(process);

                    var typedDataProvider = new DbgHelpTypedDataProvider(dbgHelp, null);

                    //Ensure we use Peb32 for the purposes of this test, which RemotePeb will resolve
                    var typedPeb = typedDataProvider.CreateObject(remotePeb.Address, "ntdll!_PEB");

                    var typedLdr = (IDbgRemoteValue) typedPeb["Ldr"].Value;
                    var remoteLdr = remotePeb.Ldr;

                    IDbgRemoteObject[] GetModuleNames(string listName, string linkName)
                    {
                        var head = (DbgRemoteListEntryHead) typedLdr[listName];

                        var list = head.ToList("ntdll!_LDR_DATA_TABLE_ENTRY", linkName);

                        return list.ToArray();
                    }

                    var typedInLoadOrder = GetModuleNames("InLoadOrderModuleList", "InLoadOrderLinks");
                    var remoteInLoadOrder = remoteLdr.InLoadOrderModuleList;

                    var typedInMemoryOrder = GetModuleNames("InMemoryOrderModuleList", "InMemoryOrderLinks");
                    var remoteInMemoryOrder = remoteLdr.InMemoryOrderModuleList;

                    var typedInInitializationOrder = GetModuleNames("InInitializationOrderModuleList", "InInitializationOrderLinks");
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

                            Assert.AreEqual(typedItem["FullDllName"].ToString().Trim('\"'), remoteItem.FullDllName);
                            Assert.AreEqual(typedItem["BaseDllName"].ToString().Trim('\"'), remoteItem.BaseDllName);
                        }
                    }
                }
            );
        }
    }
}
