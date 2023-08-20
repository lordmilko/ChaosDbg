namespace ChaosDbg.Metadata
{
    public interface ISigField
    {
        string Name { get; }

        ISigType Type { get; }

        CallingConvention CallingConvention { get; }
    }

    public class SigField : ISigField
    {
        public string Name { get; }

        public ISigType Type { get; }

        public CallingConvention CallingConvention { get; }

        public SigField(string name, ISigType type, CallingConvention callingConvention)
        {
            Name = name;
            Type = type;
            CallingConvention = callingConvention;
        }

        public override string ToString()
        {
            return $"{Type} {Name}";
        }
    }
}
