using System;
using System.Diagnostics;

namespace ChaosDbg.Metadata
{
    public interface IVS_FIXEDFILEINFO
    {
        uint Signature { get; }
        uint StrucVersion { get; }
        ushort FileVersionMinor { get; }
        ushort FileVersionMajor { get; }
        ushort FileVersionRevision { get; }
        ushort FileVersionBuild { get; }
        ushort ProductVersionMinor { get; }
        ushort ProductVersionMajor { get; }
        ushort ProductVersionRevision { get; }
        ushort ProductVersionBuild { get; }
        uint FileFlagsMask { get; }
        FileInfoFlags FileFlags { get; }
        uint FileOS { get; }
        uint FileType { get; }
        uint FileSubtype { get; }
        uint FileDateMS { get; }
        uint FileDateLS { get; }

        Version FileVersion { get; }

        Version ProductVersion { get; }
    }

    [DebuggerDisplay("File = {FileVersion.ToString(),nq}, Product = {ProductVersion.ToString(),nq}")]
    public class VS_FIXEDFILEINFO : IVS_FIXEDFILEINFO
    {
        public const uint FixedFileInfoSignature = 0xFEEF04BD;

        public uint Signature { get; }          // e.g. 0xfeef04bd
        public uint StrucVersion { get; }       // e.g. 0x00000042 = "0.42"
        public ushort FileVersionMinor { get; }
        public ushort FileVersionMajor { get; }
        public ushort FileVersionRevision { get; }
        public ushort FileVersionBuild { get; }
        public ushort ProductVersionMinor { get; }
        public ushort ProductVersionMajor { get; }
        public ushort ProductVersionRevision { get; }
        public ushort ProductVersionBuild { get; }
        public uint FileFlagsMask { get; }      // = 0x3F for version "0.42"
        public FileInfoFlags FileFlags { get; }
        public uint FileOS { get; }             // e.g. VOS_DOS_WINDOWS16
        public uint FileType { get; }           // e.g. VFT_DRIVER
        public uint FileSubtype { get; }        // e.g. VFT2_DRV_KEYBOARD
        public uint FileDateMS { get; }         // e.g. 0
        public uint FileDateLS { get; }         // e.g. 0

        public Version FileVersion => new Version(FileVersionMajor, FileVersionMinor, FileVersionBuild, FileVersionRevision);

        public Version ProductVersion => new Version(ProductVersionMajor, ProductVersionMinor, ProductVersionBuild, ProductVersionRevision);

        internal VS_FIXEDFILEINFO(PEBinaryReader reader)
        {
            Signature = reader.ReadUInt32();
            StrucVersion = reader.ReadUInt32();
            FileVersionMinor = reader.ReadUInt16();
            FileVersionMajor = reader.ReadUInt16();
            FileVersionRevision = reader.ReadUInt16();
            FileVersionBuild = reader.ReadUInt16();
            ProductVersionMinor = reader.ReadUInt16();
            ProductVersionMajor = reader.ReadUInt16();
            ProductVersionRevision = reader.ReadUInt16();
            ProductVersionBuild = reader.ReadUInt16();
            FileFlagsMask = reader.ReadUInt32();
            FileFlags = (FileInfoFlags) reader.ReadUInt32();
            FileOS = reader.ReadUInt32();
            FileType = reader.ReadUInt32();
            FileSubtype = reader.ReadUInt32();
            FileDateMS = reader.ReadUInt32();
            FileDateLS = reader.ReadUInt32();
        }
    }
}
