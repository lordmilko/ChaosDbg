using System.Text;
using ClrDebug;

namespace ChaosDbg.Metadata
{
    public interface ISigFnPtrType : ISigType
    {
        ISigMethod Method { get; }
    }

    class SigFnPtrType : SigType, ISigFnPtrType
    {
        public ISigMethod Method { get; }

        public SigFnPtrType(CorElementType type, ref SigReaderInternal reader) : base(type)
        {
            Method = reader.ParseMethod("delegate*", false);
        }

        public override string ToString()
        {
            if (Method == null)
                return base.ToString();

            var builder = new StringBuilder();

            builder.Append("delegate*<");

            foreach (var parameter in Method.Parameters)
            {
                builder.Append(parameter);

                builder.Append(", ");
            }

            builder.Append(Method.RetType);

            builder.Append(">");

            return builder.ToString();
        }
    }
}
