using System;
using System.Diagnostics;
using System.Linq;
using ChaosLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestApp;

namespace ChaosDbg.Tests
{
    [MTATestClass]
    public unsafe class TebTests : BaseTest
    {
        [TestMethod]
        public void Teb_Slot_x86() =>
            TestTebSlots(TestType.CordbEngine_Thread_TLS, true, 20);

        [TestMethod]
        public void Teb_Slot_x64() =>
            TestTebSlots(TestType.CordbEngine_Thread_TLS, false, 20);

        [TestMethod]
        public void Teb_ExpansionSlot_x86() =>
            TestTebSlots(TestType.CordbEngine_Thread_TLS_Extended, true, 100);

        [TestMethod]
        public void Teb_ExpansionSlot_x64() =>
            TestTebSlots(TestType.CordbEngine_Thread_TLS_Extended, false, 100);

        private void TestTebSlots(
            TestType testType,
            bool is32Bit,
            int maxValue)
        {
            if (IntPtr.Size == 4 && !is32Bit)
                Assert.Inconclusive("This test can only run in a 32-bit test process");

            TestCreate(
                testType,
                is32Bit,
                process =>
                {
                    var reader = new LiveProcessMemoryReader(process.Handle);

                    var thread = process.Threads.Cast<ProcessThread>().First();

                    using var handle = Kernel32.OpenThread(ThreadAccess.QUERY_INFORMATION, false, thread.Id);

                    var info = Ntdll.NtQueryInformationThread<THREAD_BASIC_INFORMATION>(handle, THREADINFOCLASS.ThreadBasicInformation);

                    var teb = RemoteTeb.FromTeb(info.TebBaseAddress, reader);

                    var slots = teb.TlsSlots.Concat(teb.TlsExpansionSlots).ToArray();

                    var found = false;

                    for (var i = 0; i < slots.Length; i++)
                    {
                        if (slots[i] == 1)
                        {
                            found = true;

                            var next = 2;

                            var j = i + 1;

                            while (next < maxValue)
                            {
                                var current = slots[j];

                                if (current == next)
                                {
                                    next++;
                                }

                                j++;
                            }

                            Assert.AreEqual(maxValue, next);

                            break;
                        }
                    }

                    Assert.IsTrue(found);
                }
            );
        }
    }
}
