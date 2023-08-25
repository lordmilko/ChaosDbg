namespace ChaosDbg.WinMD
{
    class WindowsMetadataArrayType : IWindowsMetadataType
    {
        //This can include arrays with fixed sized buffers. dotPeek doesn't seem to show the fixed size

        public IWindowsMetadataType ElementType { get; }

        public int Rank { get; }

        public int[] Sizes { get; }

        public int[] LowerBounds { get; }

        public WindowsMetadataArrayType(IWindowsMetadataType elementType, int rank, int[] sizes, int[] lowerBounds)
        {
            ElementType = elementType;
            Rank = rank;
            Sizes = sizes;
            LowerBounds = lowerBounds;
        }
    }
}
