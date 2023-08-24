namespace ChaosDbg.Metadata
{
    public interface ISigCustomAttribFixedArg
    {
    }

    class SigCustomAttribFixedArg : ISigCustomAttribFixedArg
    {
        public object Value { get; }

        public SigCustomAttribFixedArg(object value)
        {
            Value = value;
        }
    }

    class SigCustomAttribEnumFixedArg : ISigCustomAttribFixedArg
    {
        public string TypeName { get; }

        public object Value { get; }

        public SigCustomAttribEnumFixedArg(string typeName, object value)
        {
            TypeName = typeName;
            Value = value;
        }
    }

    class SigCustomAttribTypeFixedArg : ISigCustomAttribFixedArg
    {
        public string Type { get; }

        public SigCustomAttribTypeFixedArg(string type)
        {
            Type = type;
        }
    }

    class SigCustomAttribSZArrayFixedArg : ISigCustomAttribFixedArg
    {
        public ISigCustomAttribFixedArg[] Items { get; }

        public SigCustomAttribSZArrayFixedArg(ISigCustomAttribFixedArg[] items)
        {
            Items = items;
        }
    }
}
