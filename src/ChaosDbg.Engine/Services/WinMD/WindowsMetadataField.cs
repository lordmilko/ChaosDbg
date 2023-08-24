using ChaosDbg.Metadata;
using ClrDebug;

namespace ChaosDbg.WinMD
{
    class WindowsMetadataField
    {
        public mdFieldDef FieldDef { get; }

        public string Name { get; }

        public IWindowsMetadataType Type { get; }

        public ISigCustomAttribute[] CustomAttributes { get; set; }

        public WindowsMetadataField(mdFieldDef fieldDef, GetFieldPropsResult props, IWindowsMetadataType type, ISigCustomAttribute[] customAttributes)
        {
            FieldDef = fieldDef;
            Name = props.szField;
            Type = type;
            CustomAttributes = customAttributes;
        }

        public override string ToString()
        {
            return $"{Type} {Name}";
        }
    }
}
