using System;
using Iced.Intel;

namespace ChaosDbg.Disasm
{
    /// <summary>
    /// Represents a formatter capable of displaying disassembly in the DbgEng style.
    /// </summary>
    class DbgEngFormatter : IFormatterOptionsProvider
    {
        public static readonly DbgEngFormatter Default = new DbgEngFormatter();

        public Formatter Formatter { get; }

        public NumberFormattingOptions ImmediateOptions { get; }

        public DbgEngFormatter(ISymbolResolver symbolResolver = null)
        {
            Formatter = CreateFormatter(symbolResolver);
            ImmediateOptions = CreateImmediateOptions();
        }

        /// <summary>
        /// Formats a given instruction as a string.
        /// </summary>
        /// <param name="instruction">The instruction to format.</param>
        /// <param name="format">A set of options that allow customizing how the instruction is formatted.</param>
        /// <returns>The formatted instruction.</returns>
        public string Format(INativeInstruction instruction, DisasmFormatOptions format = null)
        {
            if (instruction == null)
                throw new ArgumentNullException(nameof(instruction));

            format ??= DisasmFormatOptions.Default;

            //Format the instruction in the same style as DbgEng. 

            var formatWriter = new StringOutput();

            if (!format.Simple)
            {
                //Format the instruction pointer as a 32-bit or 64-bit number depending on our target bitness
                formatWriter.Write(
                    instruction.Instruction.CodeSize == CodeSize.Code32 ?
                        Formatter.FormatInt32((int) instruction.IP, ImmediateOptions) :
                        Formatter.FormatInt64(instruction.IP, ImmediateOptions)
                );

                formatWriter.Write(" ");

                //Convert each byte into a two digit lowercase hexadecimal value.
                foreach (var @byte in instruction.Bytes)
                    formatWriter.Write(@byte.ToString("X2").ToLower());

                //Add some padding so that the instruction details are nicely aligned after the bytes
                var padding = 15 - (instruction.Bytes.Length * 2);

                for (var i = 0; i < padding; i++)
                    formatWriter.Write(" ");

                formatWriter.Write(" ");
            }

            //Now that we've processed the bytes, display the actual instruction
            Formatter.Format(instruction.Instruction, formatWriter);
            var str = formatWriter.ToStringAndReset();

            return str;
        }

        private Formatter CreateFormatter(ISymbolResolver symbolResolver)
        {
            //Masm has the most similar configuration to DbgEng so serves as the best starting point
            var opts = FormatterOptions.CreateMasm();

            //Break hexadecimal numbers into chunks of 8. e.g.
            //00007ffb`ec5cb304
            opts.HexDigitGroupSize = 8;

            //Display hex numbers in lowercase
            opts.UppercaseHex = false;

            //Don't display qualifiers that its a "near" jump, etc
            opts.ShowBranchSize = false;

            //Show "dword ptr" where appropriate
            opts.MemorySizeOptions = MemorySizeOptions.Always;

            //Character to use between each chunk of numbers, e.g.
            //00007ffb`ec5cb304
            opts.DigitSeparator = "`";

            //Display as
            //  push    1
            //instead of
            //  push 1
            opts.FirstOperandCharIndex = 8;

            //When an address is resolved to a symbol, we want to display the symbol address e.g. notepad!foo (12345678).
            //Unfortunately, Iced will use the normal hex suffix for the address enclosed in brackets. As such, we manually
            //hack this in FormattedDisasmSymbolResolver
            opts.ShowSymbolAddress = false;

            var masmFormatter = new MasmFormatter(opts, symbolResolver == null ? null : new FormattedDisasmSymbolResolver(symbolResolver), this);

            return masmFormatter;
        }

        private NumberFormattingOptions CreateImmediateOptions()
        {
            var opts = NumberFormattingOptions.CreateImmediate(Formatter.Options);
            opts.SmallHexNumbersInDecimal = false;

            //Don't include a trailing "h" on hex numbers
            opts.Suffix = null;

            //Pad numbers out to have the same number of digits, e.g. a 64-it number has 16 digits
            opts.LeadingZeros = true;

            return opts;
        }

        #region IFormatterOptionsProvider

        public void GetOperandOptions(in Instruction instruction, int operand, int instructionOperand,
            ref FormatterOperandOptions options, ref NumberFormattingOptions numberOptions)
        {
            /* instructionOperand is the one we want to look at.
             * in int 3, there is no real operand. 0xCC = "int #3", so there's
             * nothing to do.
             * in imul eax,0, we get operand 1 and instructionOperand 2.
             * The Immediate* properties all have the value 0,
             * so instructionOperand was the one to look at */

            //e.g. int 3 - there is no "3" operand, 0xCC = "int #3"
            if (instructionOperand == -1)
                return;

            var kind = instruction.GetOpKind(instructionOperand);

            //In DbgEng, we want to show operands in uppercase hex, but addresses in lowercase hex
            switch (kind)
            {
                case OpKind.Immediate8:
                case OpKind.Immediate8_2nd:
                case OpKind.Immediate16:
                case OpKind.Immediate32:
                case OpKind.Immediate64:
                case OpKind.Immediate8to16:
                case OpKind.Immediate8to32:
                case OpKind.Immediate8to64:
                    numberOptions.UppercaseHex = true;

                    var value = instruction.GetImmediate(instructionOperand);

                    //Sometimes a hex value might have a leading zero, e.g.
                    //push 0C0000409h
                    //In this scenario, we don't want a group separator, as then
                    //we'd have an ugly value like 0`C0000409h
                    if (HasStandaloneZero(value))
                        numberOptions.DigitSeparator = null;
                    
                    break;

                case OpKind.Memory:
                    if (instruction.MemoryBase != Register.None)
                    {
                        if (instruction.MemoryBase == Register.EIP || instruction.MemoryBase == Register.RIP)
                        {
                            //It's an instruction like cmp dword ptr [00007ffe`a73544a0],0
                            //We don't want to display a "h" suffix, and we need to include leading zeros
                            numberOptions.Suffix = null;

                            //In a sense, the operand is "relative to IP" (hence why IP is the MemoryBase). LeadingZeros
                            //won't do what we want - we need to use DisplacementLeadingZeros instead
                            numberOptions.DisplacementLeadingZeros = true;
                        }
                        else
                        {
                            //It's something like ebp+0Ch. We want the displacement
                            //to be capitalized. When MemoryBase is None, it could be
                            //a memory offset from some segment, which we don't care to
                            //change the formatting of
                            numberOptions.UppercaseHex = true;
                        }
                    }

                    break;
            }
        }

        private bool HasStandaloneZero(ulong value)
        {
            //Calculate how many digits are in this number
            var digits = 1;
            for (ulong tmp = value; ;)
            {
                tmp >>= 4;
                if (tmp == 0)
                    break;
                digits++;
            }

            if (digits < 17 && (int)((value >> ((digits - 1) << 2)) & 0xF) > 9)
            {
                int digit = digits >= 16 ? 0 : (int)((value >> (digits << 2)) & 0xF);

                //We've only ever seen digit be 0, so we're not sure how we want to format numbers that have a number
                //other than 0 in the 9th digit from the right
                return digit == 0;
            }

            return false;
        }

        #endregion
    }
}
