namespace ChaosDbg.WinMD
{
    class WindowsMetadataSpecialType : IWindowsMetadataType
    {
        public WindowsMetadataSpecialKind Kind { get; }

        public WindowsMetadataSpecialType(WindowsMetadataSpecialKind kind)
        {
            Kind = kind;
        }
    }
}
