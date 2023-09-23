using ChaosLib.Metadata;
using ClrDebug;

namespace ChaosDbg.WinMD
{
    class WindowsMetadataTransparentType : IWindowsMetadataType
    {
        public mdTypeDef TypeDef { get; }

        public string Name { get; }

        public string FullName { get; }

        public string Namespace { get; }

        public WindowsMetadataField ValueField { get; }

        public ISigCustomAttribute[] CustomAttributes { get; set; }

        public WindowsMetadataTransparentType(
            mdTypeDef typeDef,
            GetTypeDefPropsResult props,
            WindowsMetadataField valueField)
        {
            TypeDef = typeDef;
            FullName = props.szTypeDef;
            ValueField = valueField;

            var dot = FullName.LastIndexOf('.');

            if (dot != -1)
            {
                Name = FullName.Substring(dot + 1);
                Namespace = FullName.Substring(0, dot);
            }
            else
                Name = FullName;
        }

        public override string ToString()
        {
            return $"{Name} ({ValueField.Type})";
        }
    }
}
