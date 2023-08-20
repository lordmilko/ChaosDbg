using System.Collections.Generic;
using System.Text;
using ClrDebug;

namespace ChaosDbg.Metadata
{
    /// <summary>
    /// Represents a complex array with a specified rank. For simple single dimension arrays, SZArray is used.
    /// </summary>
    public interface ISigArrayType : ISigType
    {
        ISigType ElementType { get; }

        int Rank { get; }

        int[] Sizes { get; }

        int[] LowerBounds { get; }
    }

    /// <summary>
    /// Represents a complex array with a specified rank. For simple single dimension arrays, SZArray is used.
    /// </summary>
    class SigArrayType : SigType, ISigArrayType
    {
        public ISigType ElementType { get; }

        public int Rank { get; }

        public int[] Sizes { get; }

        public int[] LowerBounds { get; }

        public SigArrayType(CorElementType type, ref SigReaderInternal reader) : base(type)
        {
            ElementType = New(ref reader);

            Rank = reader.CorSigUncompressData();

            var numSizes = reader.CorSigUncompressData();

            var sizes = new List<int>();

            for (var i = 0; i < numSizes; i++)
                sizes.Add(reader.CorSigUncompressData());

            Sizes = sizes.ToArray();

            var numLowerBounds = reader.CorSigUncompressData();

            var lowerBounds = new List<int>();

            for (var i = 0; i < numLowerBounds; i++)
                lowerBounds.Add(reader.CorSigUncompressSignedInt());

            LowerBounds = lowerBounds.ToArray();
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.Append(ElementType).Append("[");

            for (var i = 0; i < Rank - 1; i++)
                builder.Append(",");

            builder.Append("]");

            return builder.ToString();
        }
    }
}
