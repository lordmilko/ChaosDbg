using ClrDebug;

namespace ChaosDbg.Metadata
{
    /// <summary>
    /// Represents a Single dimensional Zero based array (indices start at zero). This is _not_ a "string zero" array - the "SZ" here has nothing to do with strings, as it would in hungarian notation.
    /// </summary>
    public interface ISigSZArrayType : ISigType
    {
        ISigType ElementType { get; }
    }

    /// <summary>
    /// Represents a Single dimensional Zero based array (indices start at zero). This is _not_ a "string zero" array - the "SZ" here has nothing to do with strings, as it would in hungarian notation.
    /// </summary>
    class SigSZArrayType : SigType, ISigSZArrayType
    {
        public ISigType ElementType { get; }

        public SigSZArrayType(CorElementType type, ref SigReaderInternal reader) : base(type)
        {
            ElementType = New(ref reader);
        }

        public override string ToString()
        {
            return $"{ElementType}[]";
        }
    }
}
