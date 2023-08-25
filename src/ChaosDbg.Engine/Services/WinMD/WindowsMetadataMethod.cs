using System.Text;
using ChaosDbg.Metadata;
using ClrDebug;

namespace ChaosDbg.WinMD
{
    class WindowsMetadataMethod
    {
        public mdMethodDef MethodDef { get; }

        public string Name { get; }

        public IWindowsMetadataType ReturnType { get; }

        public IWindowsMetadataParameter[] Parameters { get; }

        public ISigCustomAttribute[] CustomAttributes { get; set; }

        public WindowsMetadataMethod(mdMethodDef methodDef, MetaDataImport_GetMethodPropsResult props, IWindowsMetadataType returnType, IWindowsMetadataParameter[] parameters, ISigCustomAttribute[] customAttributes)
        {
            MethodDef = methodDef;
            Name = props.szMethod;
            ReturnType = returnType;
            Parameters = parameters;
            CustomAttributes = customAttributes;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.Append(Name).Append("(");

            for (var i = 0; i < Parameters.Length; i++)
            {
                builder.Append(Parameters[i]);

                if (i < Parameters.Length - 1)
                    builder.Append(", ");
            }

            builder.Append(")");

            return builder.ToString();
        }
    }
}
