using System;
using System.Collections.Generic;
using System.Diagnostics;
using ChaosDbg.Metadata;

namespace ChaosDbg.Disasm
{
    /// <summary>
    /// Provides information for identifying when function code paths terminate.
    /// </summary>
    public class DisasmFunctionResolutionContext
    {
        private HashSet<long> resolutionStack = new HashSet<long>();

        private PEFunctionMetadata rootFunction;

        private PEModuleMetadata module => rootFunction.Module;

        internal DisasmFunctionResolutionContext(PEFunctionMetadata rootFunction)
        {
            this.rootFunction = rootFunction;
        }

        /// <summary>
        /// Gets the maximum address that a given function chunk may extend up to.
        /// </summary>
        public long GetFunctionChunkEndThreshold(long address) =>
            module.GetFunctionChunkEndThreshold(address);

        public bool DoesFunctionReturn(long address, bool isJump)
        {
            if (resolutionStack.Contains(address))
                return true;

            resolutionStack.Add(address);

            try
            {
                if (module.Items.TryGetValue(address, out var existing))
                {
                    if (existing is PEFunctionMetadata f)
                    {
                        //If a jmp instruction takes you to another known function, it's the end of a pathway
                        //as far as the current function is concerned
                        if (isJump)
                            return false;

                        //If we have symbol information indicating the function never returns. No need to disassemble it to check
                        //this manually
                        if (f.Symbol.DiaSymbol.NoReturn)
                            return false;

                        var oldIP = module.Disassembler.IP;

                        try
                        {
                            //IDA Pro is capable of analyzing all code paths of a function: if all paths lead to non-returning functions,
                            //then this function itself is non-returning. We don't yet have such a capability

                            f.Disassemble(this);
                            return f.Returns;
                        }
                        finally
                        {
                            module.Disassembler.IP = oldIP;
                        }

                    }
                    else
                        throw new NotImplementedException($"Don't know how to handle having a jump or call targeting an entity of type {existing}");
                }

                //If the address is a jump that we don't recognize, assume it's just another part of the function
                if (isJump)
                    return true;

#if DEBUG
                //Try and resolve the symbol straight from DbgHelp. If we get one, that means we erroneously determined that
                //it's not a function when it is, and should assert
                var symbol = module.SymbolModule.GetSymbolFromAddress(address);

                Debug.Assert(symbol == null, $"Expected 0x{address:X} to not resolve to a known symbol, however got {symbol}. This indicates this symbol is in fact a function that was erroneously excluded.");
#endif
                //In CLR modules we might be reading garbage as a call instruction.
                //Let it rip so that it eventually realizes this function is bad
                if (module.VirtualPEFile.Cor20Header != null)
                    return true;

                throw new NotImplementedException($"Don't know how to determine whether address 0x{address:X} that does not contain symbols ever returns");
            }
            finally
            {
                resolutionStack.Remove(address);
            }
        }
    }
}
