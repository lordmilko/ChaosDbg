using System;
using System.Diagnostics;
using ChaosLib;
using ClrDebug;

namespace ChaosDbg
{
    //https://learn.microsoft.com/en-us/windows/win32/api/winternl/ns-winternl-teb
    //https://www.geoffchappell.com/studies/windows/km/ntoskrnl/inc/api/pebteb/teb/index.htm

    /// <summary>
    /// Provides facilities for interacting with the TEB of a remote process.
    /// </summary>
    public class RemoteTeb
    {
        //PVOID TlsSlots[64]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int TlsSlotsFieldOffset => memoryReader.Is32Bit ? 0x0E10 : 0x1480;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private const int TlsSlotsArrayLength = 64;

        //PVOID TlsExpansionSlots
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int TlsExpansionSlotsFieldOffset => memoryReader.Is32Bit ? 0x0F94 : 0x1780;

        //PEB *ProcessEnvironmentBlock
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int ProcessEnvironmentBlockFieldOffset => memoryReader.Is32Bit ? 0x30 : 0x60;

        private const int TLS_EXPANSION_SLOTS = 1024;
        private const uint TLS_OUT_OF_INDEXES = 0xFFFFFFFF;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int ThreadLocalStoragePointerFieldOffset => memoryReader.Is32Bit ? 0x2C : 0x58;

        /// <summary>
        /// Gets the values contained in the initial 64 TlsSlots.
        /// </summary>
        public CLRDATA_ADDRESS[] TlsSlots
        {
            get
            {
                var results = new CLRDATA_ADDRESS[TlsSlotsArrayLength];

                var startAddress = Address + TlsSlotsFieldOffset;

                for (var i = 0; i < TlsSlotsArrayLength; i++)
                {
                    var slotAddress = startAddress + (memoryReader.PointerSize * i);

                    var value = memoryReader.ReadPointer(slotAddress);

                    results[i] = value;
                }

                return results;
            }
        }

        /// <summary>
        /// Gets the values contained in the additional 1024 TlsExpansionSlots.
        /// </summary>
        public CLRDATA_ADDRESS[] TlsExpansionSlots
        {
            get
            {
                var results = new CLRDATA_ADDRESS[TLS_EXPANSION_SLOTS];

                var startAddress = Address + TlsExpansionSlotsFieldOffset;

                var array = memoryReader.ReadPointer(startAddress);

                //If the array hasn't been initialized, more than 64 slots haven't been used
                if (array == 0)
                    return results;

                for (var i = 0; i < TLS_EXPANSION_SLOTS; i++)
                {
                    var slotAddress = array + (memoryReader.PointerSize * i);

                    var value = memoryReader.ReadPointer(slotAddress);

                    results[i] = value;
                }

                return results;
            }
        }

        /// <summary>
        /// Gets the address of the PEB.<para/>
        /// If we are a 64-bit process attempting to read the TEB of a 32-bit process, this will be the address of the Peb32, rather than the Wow64 PEB.
        /// </summary>
        public CLRDATA_ADDRESS ProcessEnvironmentBlock => memoryReader.ReadPointer(Address + ProcessEnvironmentBlockFieldOffset);

        /// <summary>
        /// Gets the address of the TEB.<para/>
        /// If we are a 64-bit process attempting to read the TEB of a 32-bit process, this will be the address of the Teb32, rather than the Wow64 TEB.
        /// </summary>
        public long Address { get; }

        private MemoryReader memoryReader;

        public static RemoteTeb FromThread(IntPtr hThread, MemoryReader memoryReader) => new RemoteTeb(hThread, memoryReader);

        public static RemoteTeb FromTeb(CLRDATA_ADDRESS tebAddress, MemoryReader memoryReader) => new RemoteTeb(tebAddress, memoryReader);

        private RemoteTeb(IntPtr hThread, MemoryReader memoryReader) : this(GetTebAddress(hThread), memoryReader)
        {
        }

        private RemoteTeb(CLRDATA_ADDRESS tebAddress, MemoryReader memoryReader)
        {
            if (memoryReader.Is32Bit && IntPtr.Size == 8)
            {
                /* If we're a 64-bit process attempting to interact with a 32-bit process, our supposed tebAddress
                 * actually points to the Wow64 TEB. In MmCreateTeb(), there is special logic to create a Teb32
                 * which is then stored in the NT_TIB ExceptionList property of the Wow64 TEB. Functionally speaking,
                 * this means that the start of the Wow64 TEB contains a pointer to the "true" Teb32 of the target process.
                 * WinDbg knows to dereference the TEB address as well when it sees the target is 32-bit and it itself is
                 * 64-bit. While TEB32 may apparently always be +2000 from the Wow64 TEB (https://redplait.blogspot.com/2012/12/teb32-of-wow64-process.html)
                 * it is better to the Teb32 address "properly" as WinDbg does */
                tebAddress = memoryReader.ReadPointer(tebAddress);
            }

            Address = tebAddress;
            this.memoryReader = memoryReader;
        }

        private static CLRDATA_ADDRESS GetTebAddress(IntPtr hThread)
        {
            var info = Ntdll.NtQueryInformationThread<THREAD_BASIC_INFORMATION>(hThread, THREADINFOCLASS.ThreadBasicInformation);
            return info.TebBaseAddress;
        }

        /// <summary>
        /// Gets the value contained in the specified TLS slot that was previously written to via TlsSetValue().<para/>
        /// For thread local storage that was previously embedded in the PE file, see <see cref="GetTlsPointerValue"/>
        /// </summary>
        /// <param name="index">The index of the slot to retrieve the value of.</param>
        /// <returns>The value of the specified slot.</returns>
        public long GetTlsSlotValue(int index)
        {
            if ((uint) index == TLS_OUT_OF_INDEXES)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (index > TlsSlotsArrayLength)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (index < TlsSlotsArrayLength)
                return TlsSlots[index];

            return TlsExpansionSlots[index - TlsSlotsArrayLength];
        }

        /// <summary>
        /// Gets a value from the thread local storage that was defined in the PE file and is pointed to by the
        /// ThreadLocalStoragePointer member of the TEB.<para/>
        /// For thread local storage that was manually allocated via TlsSetValue(), see <see cref="GetTlsSlotValue"/>.
        /// </summary>
        /// <returns>If a value exists at the specified index, the value at that index. Otherwise, 0.</returns>
        public long GetTlsPointerValue(int index)
        {
            var threadLocalStoragePointer = memoryReader.ReadPointer(Address + ThreadLocalStoragePointerFieldOffset);

            if (threadLocalStoragePointer == 0)
                return 0;

            var addr = threadLocalStoragePointer + (IntPtr.Size * index);

            var value = memoryReader.ReadPointer(addr);

            return value;
        }
    }
}
