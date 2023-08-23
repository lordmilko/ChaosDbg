using Iced.Intel;

namespace ChaosDbg.Disasm
{
    /// <summary>
    /// Represents a native CPU instruction and its corresponding bytes.
    /// </summary>
    public interface INativeInstruction
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
    }

    public class NativeInstruction : INativeInstruction
    {
        public long IP => (long) Instruction.IP;

        public Instruction Instruction { get; }

        public byte[] Bytes { get; }

        public NativeInstruction(Instruction instruction, byte[] bytes)
        {
            Instruction = instruction;
            Bytes = bytes;
        }

        public override string ToString()
        {
            return DbgEngFormatter.Default.Format(this);
        }
    }
}
