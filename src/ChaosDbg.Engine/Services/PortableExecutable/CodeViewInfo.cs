using System;

namespace ChaosDbg.Metadata
{
    //RSDSIH has the dwSig, guidSig, and age. Path isn't part of the struct...but I guess it follows it?

    public interface ICodeViewInfo
    {
        /// <summary>
        /// GUID (Globally Unique Identifier) of the associated PDB.
        /// </summary>
        Guid Signature { get; }

        /// <summary>
        /// Iteration of the PDB. The first iteration is 1. The iteration is incremented each time the PDB content is augmented.
        /// </summary>
        int Age { get; }

        /// <summary>
        /// Path to the .pdb file containing debug information for the PE/COFF file.
        /// </summary>
        string Path { get; }
    }

    public struct CodeViewInfo : ICodeViewInfo
    {
        /// <inheritdoc />
        public Guid Signature { get; }

        /// <inheritdoc />
        public int Age { get; }

        /// <inheritdoc />
        public string Path { get; }

        internal CodeViewInfo(PEBinaryReader reader)
        {
            if (reader.ReadByte() != (byte)'R' ||
                reader.ReadByte() != (byte)'S' ||
                reader.ReadByte() != (byte)'D' ||
                reader.ReadByte() != (byte)'S')
            {
                throw new BadImageFormatException("Unexpected CodeView data signature value.");
            }

            Signature = reader.ReadGuid();
            Age = reader.ReadInt32();
            Path = reader.ReadSZString();
        }
    }
}
