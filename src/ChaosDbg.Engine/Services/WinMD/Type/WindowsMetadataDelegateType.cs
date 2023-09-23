using ChaosLib.Metadata;
using ClrDebug;

namespace ChaosDbg.WinMD
{
    class WindowsMetadataDelegateType : IWindowsMetadataType
    {
        public mdTypeDef TypeDef { get; }

        public string Name { get; }

        public CorTypeAttr Flags { get; }

        public string FullName { get; }

        public ISigCustomAttribute[] CustomAttributes { get; set; }

        public WindowsMetadataDelegateType(
            mdTypeDef typeDef,
            GetTypeDefPropsResult props)
        {
            TypeDef = typeDef;
            FullName = props.szTypeDef;

            Flags = props.pdwTypeDefFlags;

            var dot = FullName.LastIndexOf('.');

            if (dot != -1)
                Name = FullName.Substring(dot + 1);
            else
                Name = FullName;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
