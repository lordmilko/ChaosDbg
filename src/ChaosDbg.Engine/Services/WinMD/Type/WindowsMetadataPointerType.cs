namespace ChaosDbg.WinMD
{
    class WindowsMetadataPointerType : IWindowsMetadataType
    {
        public IWindowsMetadataType PtrType { get; }

        public WindowsMetadataPointerType(IWindowsMetadataType ptrType)
        {
            PtrType = ptrType;
        }

        public override string ToString()
        {
            return $"{PtrType}*";
        }
    }
}
