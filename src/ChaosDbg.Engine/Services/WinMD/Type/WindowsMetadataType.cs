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
    }

    class WindowsMetadataType : IWindowsMetadataType
    {
        public mdTypeDef TypeDef { get; }

        public string Name { get; }

        public string FullName { get; }

        public IWindowsMetadataType BaseType { get; set; }

        public WindowsMetadataField[] Fields { get; }

        public WindowsMetadataMethod[] Methods { get; }

        public ISigCustomAttribute[] CustomAttributes { get; set; }

        public WindowsMetadataType(
            mdTypeDef typeDef,
            GetTypeDefPropsResult props,
            int numFields,
            int numMethods)
        {
            TypeDef = typeDef;
            FullName = props.szTypeDef;

            var dot = FullName.LastIndexOf('.');

            if (dot != -1)
                Name = FullName.Substring(dot + 1);
            else
                Name = FullName;

            Fields = numFields == 0 ? Array.Empty<WindowsMetadataField>() : new WindowsMetadataField[numFields];
            Methods = numMethods == 0 ? Array.Empty<WindowsMetadataMethod>() : new WindowsMetadataMethod[numMethods];
        }

        public override string ToString()
        {
            if (Name == "Apis")
                return FullName;

            return Name;
        }
    }
}
