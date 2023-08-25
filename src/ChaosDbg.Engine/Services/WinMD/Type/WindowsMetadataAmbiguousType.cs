namespace ChaosDbg.WinMD
{
    class WindowsMetadataAmbiguousType : IWindowsMetadataType
    {
        public IWindowsMetadataType[] Candidates { get; }

        public WindowsMetadataAmbiguousType(IWindowsMetadataType[] candidates)
        {
            Candidates = candidates;
        }
    }
}
