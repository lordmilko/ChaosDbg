﻿using System.Diagnostics;
using ChaosDbg.Analysis;
using Iced.Intel;

namespace ChaosDbg.Disasm
{
    /// <summary>
    /// Represents a native CPU instruction and its corresponding bytes.
    /// </summary>
    public interface INativeInstruction : IInstruction //We need this because we have MockInstruction for graph tests
    {
        long IP { get; }

        /// <summary>
        /// Gets the raw instruction that was read by the disassembler.
        /// </summary>
        Instruction Instruction { get; }

        /// <summary>
        /// Gets the bytes that comprise the read <see cref="Instruction"/>.
        /// </summary>
        byte[] Bytes { get; }

        string ToString(DisasmFormatOptions format);
    }

    public class NativeInstruction : INativeInstruction
    {
        long IInstruction.Address => IP;

        public long IP => (long) Instruction.IP;

        public Instruction Instruction { get; }

        public byte[] Bytes { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private DbgEngFormatter formatter;

        internal NativeInstruction(in Instruction instruction, byte[] bytes, DbgEngFormatter formatter)
        {
            Instruction = instruction;
            Bytes = bytes;
            this.formatter = formatter;
        }

        internal XRefAwareNativeInstruction ToXRefAware() => new XRefAwareNativeInstruction(Instruction, Bytes, formatter);

        public override string ToString() => formatter.Format(this);

        public string ToString(DisasmFormatOptions format) => formatter.Format(this, format);
    }
}
