namespace ChaosDbg.WinMD
{
    class WindowsMetadataSZArrayType : IWindowsMetadataType
    {
        public IWindowsMetadataType ElementType { get; }

        public WindowsMetadataSZArrayType(IWindowsMetadataType elementType)
        {
            ElementType = elementType;
        }

        public override string ToString()
        {
            return $"{ElementType}[]";
        }
    }
}
