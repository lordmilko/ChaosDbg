using System;
using ChaosDbg.Disasm;
using ClrDebug.DbgEng;
using Iced.Intel;

namespace ChaosDbg.DbgEng
{
    public class DbgEngDisasmSymbolResolver : IIndirectSymbolResolver
    {
        private readonly DebugClient client;

        public NativeDisassembler ProcessDisassembler { get; set; }

        public DbgEngDisasmSymbolResolver(DebugClient client)
        {
            this.client = client;
        }

        public bool TryGetSymbol(in Instruction instruction, int operand, int instructionOperand, ulong address, int addressSize, out SymbolResult symbol)
        {
            var hr = client.Symbols.TryGetNameByOffset((long) address, out var result);

            if (hr != ClrDebug.HRESULT.S_OK)
            {
                symbol = default;
                return false;
            }

            //If the symbol is foo+0x100, the symbol foo is -0x100 from the symbol we resolved. Iced will see that the output address
            //is different from the input address and add +0x100 to the resulting output
            symbol = new SymbolResult(address - (ulong) result.Displacement, result.NameBuffer);

            return true;
        }


        public bool TryGetIndirectSymbol(in Instruction instruction, ulong address, int addressSize, out ulong targetAddress, out SymbolResult symbol)
        {
            throw new NotImplementedException();
        }
    }
}
