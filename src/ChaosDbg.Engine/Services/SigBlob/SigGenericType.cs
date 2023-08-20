using System.Collections.Generic;
using System.Text;
using ClrDebug;

namespace ChaosDbg.Metadata
{
    public interface ISigGenericType : ISigType
    {
        string GenericTypeDefinitionName { get; }

        mdToken GenericTypeDefinitionToken { get; }

        ISigType[] GenericArgs { get; }
    }

    class SigGenericType : SigType, ISigGenericType
    {
        public string GenericTypeDefinitionName { get; }

        public mdToken GenericTypeDefinitionToken { get; }

        public ISigType[] GenericArgs { get; }

        public SigGenericType(CorElementType type, ref SigReaderInternal reader) : base(type)
        {
            GenericTypeDefinitionToken = reader.CorSigUncompressToken();
            GenericTypeDefinitionName = GetName(GenericTypeDefinitionToken, reader.Import);
            var genericArgsLength = reader.CorSigUncompressData();

            var args = new List<ISigType>();

            for (var i = 0; i < genericArgsLength; i++)
                args.Add(New(ref reader));

            GenericArgs = args.ToArray();
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            var name = GenericTypeDefinitionName;

            var index = name.IndexOf('`');

            if (index != -1)
                name = name.Substring(0, index);

            builder.Append(name);
            builder.Append("<");

            for (var i = 0; i < GenericArgs.Length; i++)
            {
                builder.Append(GenericArgs[i]);

                if (i < GenericArgs.Length - 1)
                    builder.Append(", ");
            }

            builder.Append(">");

            return builder.ToString();
        }
    }
}
