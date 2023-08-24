using ClrDebug;

namespace ChaosDbg.WinMD
{
    class WindowsMetadataPrimitiveType : IWindowsMetadataType
    {
        public CorElementType Type { get; }

        public WindowsMetadataPrimitiveType(CorElementType type)
        {
            Type = type;
        }

        public override string ToString()
        {
            return Type.ToString();
        }
    }
}
