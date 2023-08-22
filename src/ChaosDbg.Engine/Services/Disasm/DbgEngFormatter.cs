using Iced.Intel;

namespace ChaosDbg.Disasm
{
    /// <summary>
    /// Represents a formatter capable of displaying disassembly in the DbgEng style.
    /// </summary>
    class DbgEngFormatter
    {
        public static readonly DbgEngFormatter Default = new DbgEngFormatter();

        public MasmFormatter Formatter { get; }

        public NumberFormattingOptions ImmediateOptions { get; }

        private ISymbolResolver symbolResolver;

        public DbgEngFormatter(ISymbolResolver symbolResolver = null)
        {
            Formatter = CreateFormatter();
            ImmediateOptions = CreateImmediateOptions();

            this.symbolResolver = symbolResolver;
        }

        private MasmFormatter CreateFormatter()
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

            //Character to use between each chunk of numbers, e.g.
            //00007ffb`ec5cb304
            opts.DigitSeparator = "`";

            //Display as
            //  push    1
            //instead of
            //  push 1
            opts.FirstOperandCharIndex = 8;

            var masmFormatter = new MasmFormatter(opts, symbolResolver, DbgEngOptionsProvider.Instance);

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
    }
}
