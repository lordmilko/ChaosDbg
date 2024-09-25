using Iced.Intel;

namespace ChaosDbg.Disasm
{
    interface IIndirectSymbolResolver : ISymbolResolver
    {
        //Must have a stream that has access to read the entire contents of the process
        NativeDisassembler ProcessDisassembler { get; set; }

        bool TryGetIndirectSymbol(in Instruction instruction, ulong address, int addressSize, out ulong targetAddress, out SymbolResult symbol);
    }
}
