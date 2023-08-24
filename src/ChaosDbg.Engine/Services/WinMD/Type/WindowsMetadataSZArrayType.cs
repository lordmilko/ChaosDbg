namespace ChaosDbg.WinMD
{
    class WindowsMetadataSZArrayType : IWindowsMetadataType
    {
        public WindowsMetadataSZArrayType(IWindowsMetadataType elementType)
        {
            ElementType = elementType;
        }

        public IWindowsMetadataType ElementType { get; }
    }
}
