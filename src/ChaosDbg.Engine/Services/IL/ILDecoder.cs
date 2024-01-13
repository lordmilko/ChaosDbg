using System;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using ChaosLib.Metadata;

namespace ChaosDbg.IL
{
    /// <summary>
    /// Decodes Common Intermediate Language instructions.
    /// </summary>
    public class ILDecoder
    {
        private static readonly OpCode[] singleByteOpCodes = new OpCode[256];

        //Currently, all multi-byte opcodes have Prefix1 in their high byte
        private static readonly OpCode[] multiByteOpCodes = new OpCode[256];

        //https://github.com/dotnet/runtime/blob/53400d0c25f5858083cf8892dc89bd19674a622c/src/coreclr/inc/opcode.def#L20
        private const byte Prefix7 = 0xF8;
        private const byte Prefix6 = 0xF9;
        private const byte Prefix5 = 0xFA;
        private const byte Prefix4 = 0xFB;
        private const byte Prefix3 = 0xFC;
        private const byte Prefix2 = 0xFD;
        private const byte Prefix1 = 0xFE;
        private const byte PrefixRef = 0xFF;

        static ILDecoder()
        {
            var opcodes = typeof(OpCodes).GetFields().Select(f => (OpCode) f.GetValue(null)).ToArray();

            foreach (var opcode in opcodes)
            {
                //Opcode values are stored in OpCode as short, but they're really ushort, and may be negative if we were to treat them as mere short's
                var value = (ushort) opcode.Value;

                //Opcodes are stored using either 1 or 2 bytes. Opcodes whose value is lower than 256 are 1 byte
                if (value < 256)
                    singleByteOpCodes[value] = opcode;
                else
                {
                    //When an opcode's value is >= 256, this indicates it is a multibyte opcode. The Common Intermediate Language reserves a number of special
                    //"prefix" opcodes for signifying that in fact this is a multibyte opcode, and that you should direct your attention to subsequent bytes for figuring out what
                    //overall instruction we're trying to signify this is. Currently, only the "Prefix1" (FE) opcode is used to signify an opcode utilizes 2 bytes

                    //Get the high order byte
                    var hiByte = (value & 0xff00) >> 8;

                    //Get the low order byte
                    var loByte = value & 0x00ff;

                    if (hiByte != Prefix1) //If the first byte is not 0xfe ("Prefix1") then this is an unknown multibyte prefix we don't know how to deal with
                        throw new NotImplementedException($"Don't know how to handle multibyte opcode with first byte '0x{hiByte:X}'");

                    multiByteOpCodes[loByte] = opcode;
                }
            }
        }

        private BinaryReader reader;
        private MetaDataProvider provider;

        public ILDecoder(Stream stream, MetaDataProvider provider)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            this.reader = new BinaryReader(stream);
            this.provider = provider;
        }

        public ILInstruction Decode()
        {
            var offset = reader.BaseStream.Position;
            var opcode = ReadOpCode();
            var operand = ReadOperand(opcode);

            return new ILInstruction(
                (int) offset,
                (int) (reader.BaseStream.Position - offset),
                opcode,
                operand
            );
        }

        private OpCode ReadOpCode()
        {
            var value = reader.ReadByte();

            switch (value)
            {
                case Prefix1:
                    var lo = reader.ReadByte();
                    return multiByteOpCodes[lo];

                case Prefix2:
                case Prefix3:
                case Prefix4:
                case Prefix5:
                case Prefix6:
                case Prefix7:
                case PrefixRef:
                    throw new NotImplementedException($"Don't know how to handle prefix opcode '{value}'");

                default:
                    return singleByteOpCodes[value];
            }
        }

        private object ReadOperand(OpCode opcode)
        {
            switch (opcode.OperandType)
            {
                case OperandType.InlineNone:
                    return null;

                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                    var token = reader.ReadInt32();
                    return provider.ResolveToken(token);

                case OperandType.InlineI:
                case OperandType.InlineBrTarget:
                    return reader.ReadInt32();

                case OperandType.InlineI8:
                    return reader.ReadInt64();

                case OperandType.InlineR:
                    return reader.ReadDouble();

                case OperandType.ShortInlineBrTarget:
                    return reader.ReadSByte() + reader.BaseStream.Position;

                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    return reader.ReadByte();

                default:
                    throw new NotImplementedException($"Don't know how to handle {nameof(OperandType)} '{opcode.OperandType}'.");
            }
        }
    }
}
