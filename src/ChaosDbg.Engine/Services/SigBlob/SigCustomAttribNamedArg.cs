using ClrDebug;

namespace ChaosDbg.Metadata
{
    public interface ISigCustomAttribNamedArg
    {
    }

    class SigCustomAttribNamedArg : ISigCustomAttribNamedArg
    {
        public CorSerializationType MemberKind { get; }

        public string Name { get; }

        public ISigCustomAttribFixedArg Value { get; }

        public SigCustomAttribNamedArg(CorSerializationType memberKind, string name, ISigCustomAttribFixedArg value)
        {
            MemberKind = memberKind;
            Name = name;
            Value = value;
        }
    }
}
