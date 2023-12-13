using System;
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
        private int TlsSlotsFieldOffset => memoryReader.Is32Bit ? 0x0E10 : 0x1480;
        private const int TlsSlotsArrayLength = 64;

        //PVOID TlsExpansionSlots
        private int TlsExpansionSlotsFieldOffset => memoryReader.Is32Bit ? 0x0F94 : 0x1780;

        private const int TLS_EXPANSION_SLOTS = 1024;
        private const uint TLS_OUT_OF_INDEXES = 0xFFFFFFFF;

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
        /// Gets the address of the TEB.<para/>
        /// If we are a 64-bit process attempting to read the TEB of a 32-bit process, this will be the address of the Teb32, rather than the Wow64 TEB.
        /// </summary>
        public long Address { get; }

        private MemoryReader memoryReader;

        public RemoteTeb(long tebAddress, MemoryReader memoryReader)
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

        public long GetTlsValue(int index)
        {
            if ((uint) index == TLS_OUT_OF_INDEXES)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (index < TlsSlotsArrayLength)
                return TlsSlots[index];

            return TlsExpansionSlots[index - TlsSlotsArrayLength];
        }
    }
}
