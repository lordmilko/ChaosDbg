using ChaosLib.Symbols;
using Iced.Intel;

namespace ChaosDbg.Disasm
{
    class SymbolProviderDisasmSymbolResolver : IIndirectSymbolResolver
    {
        private readonly SymbolProvider symbolProvider;

        public NativeDisassembler ProcessDisassembler { get; set; }

        public SymbolProviderDisasmSymbolResolver(SymbolProvider symbolProvider)
        {
            this.symbolProvider = symbolProvider;
        }

        public bool TryGetSymbol(in Instruction instruction, int operand, int instructionOperand, ulong address, int addressSize,
            out SymbolResult symbol)
        {
            if (!symbolProvider.TryGetSymbolFromAddress((long) address, out var result))
            {
                symbol = default;
                return false;
            }

            //If the symbol is foo+0x100, the symbol foo is -0x100 from the symbol we resolved. Iced will see that the output address
            //is different from the input address and add +0x100 to the resulting output
            symbol = new SymbolResult(address - (ulong) result.Displacement, result.Name);

            return true;
        }

        public bool TryGetIndirectSymbol(in Instruction instruction, ulong address, int addressSize, out ulong targetAddress, out SymbolResult symbol)
        {
            targetAddress = default;
            symbol = default;
            return false;
        }
    }
}
