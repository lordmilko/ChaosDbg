using System;

namespace ChaosDbg
{
    public class AddressChangedEventArgs : EventArgs
    {
        public long Address { get; }

        public AddressChangedEventArgs(long address)
        {
            Address = address;
        }
    }
}
