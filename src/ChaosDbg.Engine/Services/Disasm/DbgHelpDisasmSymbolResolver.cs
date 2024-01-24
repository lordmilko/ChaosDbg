using ChaosLib;
using ClrDebug;
using Iced.Intel;

namespace ChaosDbg.Disasm
{
    class DbgHelpDisasmSymbolResolver : ISymbolResolver
    {
        private readonly DbgHelpSession dbgHelpSession;

        public DbgHelpDisasmSymbolResolver(DbgHelpSession dbgHelpSession)
        {
            this.dbgHelpSession = dbgHelpSession;
        }

        public bool TryGetSymbol(in Instruction instruction, int operand, int instructionOperand, ulong address, int addressSize,
            out SymbolResult symbol)
        {
            if (dbgHelpSession.TrySymFromAddr((long) address, out var result) != HRESULT.S_OK)
            {
                symbol = default;
                return false;
            }

            //If the symbol is foo+0x100, the symbol foo is -0x100 from the symbol we resolved. Iced will see that the output address
            //is different from the input address and add +0x100 to the resulting output
            symbol = new SymbolResult(address - (ulong) result.Displacement, result.SymbolInfo.Name);

            return true;
        }
    }
}
