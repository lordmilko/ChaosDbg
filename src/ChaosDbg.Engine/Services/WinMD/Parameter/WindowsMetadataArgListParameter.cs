namespace ChaosDbg.WinMD
{
    class WindowsMetadataArgListParameter : IWindowsMetadataParameter
    {
        public IWindowsMetadataType Type { get; }

        public override string ToString()
        {
            return "__arglist";
        }
    }
}
