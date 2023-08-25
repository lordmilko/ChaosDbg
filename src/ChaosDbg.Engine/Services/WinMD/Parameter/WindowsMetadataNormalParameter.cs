using ClrDebug;

namespace ChaosDbg.WinMD
{
    class WindowsMetadataNormalParameter : IWindowsMetadataParameter
    {
        public string Name;

        public IWindowsMetadataType Type { get; }

        public WindowsMetadataNormalParameter(GetParamPropsResult? info, IWindowsMetadataType type)
        {
            Name = info?.szName;
            Type = type;
        }

        public override string ToString()
        {
            if (Name == null)
                return Type.ToString();

            return $"{Type} {Name}";
        }
    }
}
