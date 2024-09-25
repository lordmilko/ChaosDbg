using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ChaosDbg.Cordb;
using ChaosLib.Metadata;
using ClrDebug;

#nullable enable

namespace ChaosDbg.IL
{
    /// <summary>
    /// Represents an engine capable of disassembling Common Intermediate Language instructions.
    /// </summary>
    public class ILDisassembler
    {
        private ILDecoder Decoder { get; }

        private readonly Stream stream;

        public static ILInstruction[] Disassemble(MethodBase method) =>
            Disassemble(method.GetMethodBody().GetILAsByteArray());

        public static ILInstruction[] Disassemble(byte[] ilBytes)
        {
            var disassembler = Create(ilBytes);

            return disassembler.EnumerateInstructions().ToArray();
        }

        public static ILDisassembler Create(CorDebugFunction corDebugFunction, CordbManagedModule module)
        {
            var code = corDebugFunction.ILCode;

            /* MSDN says GetCode() is deprecated, and to use ICorDebugCode2::GetCodeChunks instead.
             * There are two types of objects that can underpin ICorDebugCode:
             * - CordbCode, which only implements ICorDebugCode, and
             * - CordbNativeCode, which implements all ICorDebugCode interfaces
             *
             * Therefore, you can't necessarily use GetCodeChunks if you have non-native code */

            var size = code.Size;
            var bytes = code.GetCode(0, size, size);

            var stream = new MemoryStream(bytes);

            return new ILDisassembler(stream, module.MetadataModule);
        }

        public static ILDisassembler Create(byte[] ilBytes)
        {
            if (ilBytes == null)
                throw new ArgumentNullException(nameof(ilBytes));

            var stream = new MemoryStream(ilBytes);

            return new ILDisassembler(stream, null);
        }

        public static ILDisassembler Create(byte[] ilBytes, MetadataModule metadataModule)
        {
            if (ilBytes == null)
                throw new ArgumentNullException(nameof(ilBytes));

            if (metadataModule == null)
                throw new ArgumentNullException(nameof(metadataModule));

            var stream = new MemoryStream(ilBytes);

            return new ILDisassembler(stream, metadataModule);
        }

        private ILDisassembler(Stream stream, MetadataModule? metadataModule)
        {
            this.stream = stream;

            Decoder = new ILDecoder(stream, metadataModule);
        }

        public IEnumerable<ILInstruction> EnumerateInstructions()
        {
            //Always begin from the beginning of the stream
            stream.Position = 0;

            var results = new List<ILInstruction>();

            while (true)
            {
                if (stream.Position >= stream.Length)
                    break;

                var instr = Decoder.Decode();
                results.Add(instr);
            }

            var totalOffset = 0;

            var offsetMap = new Dictionary<int, ILInstruction>();

            foreach (var instr in results)
            {
                offsetMap.Add(totalOffset, instr);

                totalOffset += instr.Length;
            }

            ILInstruction GetBranchTarget(int offset)
            {
                if (offsetMap.TryGetValue(offset, out var instr))
                    return instr;

                throw new NotImplementedException($"Could not find the instruction pointed to by offset {offset}");
            }

            ILVariable GetVariable(ILInstruction instr)
            {
                if (ILDecoder.localOpCodes.Contains(instr.OpCode))
                    return new ILVariable(ILVariableKind.Local, Convert.ToInt16(instr.Operand));

                if (ILDecoder.parameterOpCodes.Contains(instr.OpCode))
                    return new ILVariable(ILVariableKind.Parameter, Convert.ToInt16(instr.Operand));

                throw new NotImplementedException($"Don't know whether instruction {instr} is a local or parameter");
            }

            foreach (var instr in results)
            {
                switch (instr.OpCode.OperandType)
                {
                    case OperandType.InlineBrTarget:
                    case OperandType.ShortInlineBrTarget:
                        instr.Operand = GetBranchTarget((int) instr.Operand);
                        break;

                    case OperandType.InlineSwitch:
                    {
                        var existing = (int[]) instr.Operand;

                        var targets = new ILInstruction[existing.Length];

                        for (var i = 0; i < existing.Length; i++)
                            targets[i] = GetBranchTarget(existing[i]);

                        instr.Operand = targets;
                        break;
                    }

                    case OperandType.InlineVar:
                    case OperandType.ShortInlineVar:
                        instr.Operand = GetVariable(instr);
                        break;
                }
            }

            return results;
        }
    }
}
