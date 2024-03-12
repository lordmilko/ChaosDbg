using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using ChaosLib;
using ClrDebug;

namespace ChaosDbg
{
    /// <summary>
    /// Provides facilities for interacting with the PEB of a remote process.
    /// </summary>
    public class RemotePeb
    {
        public PebLdrData Ldr =>
            new PebLdrData(memoryReader.ReadPointer(Address + (memoryReader.PointerSize * 3)), memoryReader);

        public PebUserProcessParameters ProcessParameters =>
            new PebUserProcessParameters(memoryReader.ReadPointer(Address + (memoryReader.PointerSize * 4)), memoryReader);

        public long Address { get; }

        private MemoryReader memoryReader;

        public RemotePeb(CLRDATA_ADDRESS pebAddress, MemoryReader memoryReader)
        {
            if (memoryReader.Is32Bit && IntPtr.Size == 8)
            {
                /* If we're a 64-bit process attempting to interact with a 32-bit process, our supposed pebAddress
                 * actually points to a Wow64 PEB. Key members such as Ldr will point to -1. There doesn't seem to
                 * be a good way to get the address of the Peb32 beyond resolving the Teb32 and then accessing the
                 * ProcessEnvironmentBlock. This is what DbgEng does as well. */

                //First we need to get a thread

                var thread = Process.GetProcessById(Kernel32.GetProcessId(memoryReader.hProcess)).Threads.Cast<ProcessThread>().First();

                using var handle = Kernel32.OpenThread(ThreadAccess.QUERY_INFORMATION, false, thread.Id);

                var info = Ntdll.NtQueryInformationThread<THREAD_BASIC_INFORMATION>(handle, THREADINFOCLASS.ThreadBasicInformation);

                var remoteTeb = RemoteTeb.FromTeb(info.TebBaseAddress, memoryReader);

                pebAddress = remoteTeb.ProcessEnvironmentBlock;
            }

            Address = pebAddress;
            this.memoryReader = memoryReader;
        }

        public RemotePeb(Process process) : this(GetPebAddress(process), new MemoryReader(process.Handle))
        {
        }

        private static CLRDATA_ADDRESS GetPebAddress(Process process)
        {
            var info = Ntdll.NtQueryInformationProcess<PROCESS_BASIC_INFORMATION>(process.Handle, PROCESSINFOCLASS.ProcessBasicInformation);

            return info.PebBaseAddress;
        }

        #region Types
        #region Ldr

        public class PebLdrData
        {
            #region Field Offsets

            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private int InLoadOrderModuleListFieldOffset => memoryReader.Is32Bit ? 0x0C : 0x10;
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private int InLoadOrderLinksFieldOffset => 0;
            
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private int InMemoryOrderModuleListFieldOffset => memoryReader.Is32Bit ? 0x14 : 0x20;
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private int InMemoryOrderLinksFieldOffset => memoryReader.Is32Bit ? 0x08 : 0x10;
            
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private int InInitializationOrderModuleListFieldOffset => memoryReader.Is32Bit ? 0x1C : 0x30;
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private int InInitializationOrderLinksFieldOffset => memoryReader.Is32Bit ? 0x10 : 0x20;

            #endregion

            //The head item on the PEB_LDR_DATA has the suffix "ModuleList", and then each LDR_DATA_TABLE_ENTRY links
            //to each to its peers with the suffix "Links"
            public LdrDataTableEntry[] InLoadOrderModuleList =>
                ReadLinkedList(InLoadOrderModuleListFieldOffset, InLoadOrderLinksFieldOffset);
            public LdrDataTableEntry[] InMemoryOrderModuleList =>
                ReadLinkedList(InMemoryOrderModuleListFieldOffset, InMemoryOrderLinksFieldOffset);
            public LdrDataTableEntry[] InInitializationOrderModuleList =>
                ReadLinkedList(InInitializationOrderModuleListFieldOffset, InInitializationOrderLinksFieldOffset);

            public long Address { get; }

            private MemoryReader memoryReader;

            public PebLdrData(long address, MemoryReader memoryReader)
            {
                Address = address;
                this.memoryReader = memoryReader;
            }

            private LdrDataTableEntry[] ReadLinkedList(int listHeadOffset, int listEntryOffset)
            {
                //We always include the offset to the first list field and then for each of the 3 lists, we offset further from that
                var headAddr = Address + listHeadOffset;
                var headFlink = memoryReader.ReadPointer(headAddr); //We can't read LIST_ENTRY because LIST_ENTRY uses IntPtr

                var currentFlink = headFlink;

                var dllBaseFieldOffset = memoryReader.Is32Bit ? 0x18 : 0x30;
                var entryPointFieldOffset = memoryReader.Is32Bit ? 0x1C : 0x38;
                var sizeOfImageFieldOffset = memoryReader.Is32Bit ? 0x20 : 0x40;
                var fullDllNameFieldOffset = memoryReader.Is32Bit ? 0x24 : 0x48;
                var baseDllNameFieldOffset = memoryReader.Is32Bit ? 0x2C : 0x58;

                var results = new List<LdrDataTableEntry>();

                do
                {
                    var objectStart = currentFlink - listEntryOffset;

                    var dllBase = memoryReader.ReadPointer(objectStart + dllBaseFieldOffset);
                    var entryPoint = memoryReader.ReadPointer(objectStart + entryPointFieldOffset);
                    var sizeOfImage = memoryReader.ReadVirtual<int>(objectStart + sizeOfImageFieldOffset);
                    var fullDllName = memoryReader.ReadUnicodeString(objectStart + fullDllNameFieldOffset);
                    var baseDllName = memoryReader.ReadUnicodeString(objectStart + baseDllNameFieldOffset);

                    results.Add(new LdrDataTableEntry(dllBase, entryPoint, sizeOfImage, fullDllName, baseDllName));

                    currentFlink = memoryReader.ReadPointer(currentFlink);
                } while (currentFlink != headAddr);

                return results.ToArray();
            }
        }

        public class LdrDataTableEntry
        {
            public long DllBase { get; }

            public long EntryPoint { get; }

            public int SizeOfImage { get; }

            public string FullDllName { get; }

            public string BaseDllName { get; }

            public LdrDataTableEntry(long dllBase, long entryPoint, int sizeOfImage, string fullDllName, string baseDllName)
            {
                DllBase = dllBase;
                EntryPoint = entryPoint;
                SizeOfImage = sizeOfImage;
                FullDllName = fullDllName;
                BaseDllName = baseDllName;
            }

            public override string ToString()
            {
                return FullDllName;
            }
        }

        #endregion
        #region ProcessParameters

        public class PebUserProcessParameters
        {
            #region Field Offsets

            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private int CommandLineFieldOffset => memoryReader.Is32Bit ? 0x40 : 0x70;

            #endregion

            public string CommandLine =>
                memoryReader.ReadUnicodeString(Address + CommandLineFieldOffset);

            public long Address { get; }

            private MemoryReader memoryReader;

            public PebUserProcessParameters(long address, MemoryReader memoryReader)
            {
                Address = address;
                this.memoryReader = memoryReader;
            }
        }

        #endregion
        #endregion
    }
}
