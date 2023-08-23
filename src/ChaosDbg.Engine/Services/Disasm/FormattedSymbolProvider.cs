using System;
using System.Text;
using Iced.Intel;

namespace ChaosDbg.Disasm
{
    class FormattedSymbolProvider : ISymbolResolver
    {
        private readonly ISymbolResolver symbolResolver;
        private Formatter displacementFormatter;
        private NumberFormattingOptions displacementOpts;
        private NumberFormattingOptions addressOpts;

        public FormattedSymbolProvider(ISymbolResolver symbolResolver)
        {
            if (symbolResolver == null)
                throw new ArgumentNullException(nameof(symbolResolver));

            this.symbolResolver = symbolResolver;

            displacementFormatter = new MasmFormatter();

            displacementOpts = new NumberFormattingOptions()
            {
                Prefix = "0x",
                Suffix = null,
            };

            addressOpts = new NumberFormattingOptions
            {
                DigitSeparator = "`",
                DigitGroupSize = 8,
                LeadingZeros = true,
                Suffix = null
            };
        }

        public bool TryGetSymbol(in Instruction instruction, int operand, int instructionOperand, ulong address, int addressSize,
            out SymbolResult symbol)
        {
            var result = symbolResolver.TryGetSymbol(instruction, operand, instructionOperand, address, addressSize, out symbol);

            if (result)
            {
                //We want to show a "h" prefix on hex literals, but we don't want to do that when displaying the symbol address,
                //so we created a custom formatter here that can manually append the symbol address in the format we want
                var symbolName = symbol.Text.Text.Text;

                var builder = new StringBuilder();
                builder.Append(symbolName);

                var displacement = (long)address - (long)symbol.Address;

                if (displacement != 0)
                {
                    //We want to display displacements as 0x<address>, and also without a "h" suffix.
                    //Iced's built-in formatter can't handle this either, so we manually handle this here
                    if (displacement > 0)
                        builder.Append("+");

                    builder.Append(displacementFormatter.FormatInt64(displacement, displacementOpts));
                }

                builder.Append(" ").Append("(");

                var addr = instruction.CodeSize == CodeSize.Code32 ?
                    displacementFormatter.FormatInt32((int)address, addressOpts) :
                    displacementFormatter.FormatInt64((long)address, addressOpts);

                builder.Append(addr);

                builder.Append(")");

                symbol = new SymbolResult(address, builder.ToString());
            }

            return result;
        }
    }
}
