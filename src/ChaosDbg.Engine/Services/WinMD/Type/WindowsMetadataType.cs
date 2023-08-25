using System;
using ChaosDbg.Metadata;
using ClrDebug;

namespace ChaosDbg.WinMD
{
    interface IWindowsMetadataType
    {
    }

    class WindowsMetadataTypeInternal : IWindowsMetadataType
    {
        internal static readonly IWindowsMetadataType DeleteInheritedType = new WindowsMetadataTypeInternal();

        internal static readonly IWindowsMetadataType MulticastDelegateType = new WindowsMetadataTypeInternal();

        internal static readonly IWindowsMetadataType EnumType = new WindowsMetadataTypeInternal();
    }

    class WindowsMetadataType : IWindowsMetadataType
    {
        public mdTypeDef TypeDef { get; }

        public string Name { get; }

        public string FullName { get; }

        public CorTypeAttr Flags { get; }

        public IWindowsMetadataType ParentType { get; set; }

        public IWindowsMetadataType BaseType { get; set; }

        public WindowsMetadataField[] Fields => fields?.Value;

        public WindowsMetadataMethod[] Methods => methods?.Value;

        public ISigCustomAttribute[] CustomAttributes { get; set; }

        private Lazy<WindowsMetadataField[]> fields;
        private Lazy<WindowsMetadataMethod[]> methods;

        public WindowsMetadataType(
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

        public void SetFields(Lazy<WindowsMetadataField[]> fields) => this.fields = fields;
        public void SetMethods(Lazy<WindowsMetadataMethod[]> methods) => this.methods = methods;

        public override string ToString()
        {
            if (Name == "Apis")
                return FullName;

            return Name;
        }
    }
}
