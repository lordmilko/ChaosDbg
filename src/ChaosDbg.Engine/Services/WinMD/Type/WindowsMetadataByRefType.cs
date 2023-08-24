namespace ChaosDbg.WinMD
{
    class WindowsMetadataByRefType : IWindowsMetadataType
    {
        public IWindowsMetadataType InnerType { get; }

        public WindowsMetadataByRefType(IWindowsMetadataType innerType)
        {
            InnerType = innerType;
        }
    }
}
