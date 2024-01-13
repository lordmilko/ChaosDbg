using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ChaosLib;
using ClrDebug;
using Iced.Intel;
using static ClrDebug.HRESULT;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Provides facilities for resolving managed addresses to symbol identifiers when disassembling managed code.
    /// </summary>
    public class CordbDisasmSymbolResolver : ISymbolResolver
    {
        private CordbProcess process;
        private SOSDacInterface sos;
        private DacpUsefulGlobalsData usefulGlobals;

        //We can't use Lazy<T> here, because asking for the value will call Debugger.NotifyOfCrossThreadDependency()
        //which can cause a ThreadAbortException to cancel the debugger display.
        private Tuple<ProcessModule, MODULEINFO?>[] processModules;
        private Tuple<ProcessModule, MODULEINFO?>[] ProcessModules
        {
            get
            {
                if (processModules == null)
                {
                    //Always get a fresh Process object to ensure our Modules list is up to date
                    var modules = Process.GetProcessById(process.Id).Modules.Cast<ProcessModule>().ToArray();

                    processModules = modules.Select(v => Tuple.Create<ProcessModule, MODULEINFO?>(v, null)).ToArray();
                }

                return processModules;
            }
        }

        public CordbDisasmSymbolResolver(CordbProcess process)
        {
            this.process = process;
            this.sos = process.DAC.SOS;
            usefulGlobals = sos.UsefulGlobals;
        }

        public unsafe bool TryGetSymbol(in Instruction instruction, int operand, int instructionOperand, ulong address, int addressSize, out SymbolResult symbol)
        {
            //Based on dnSpy ClrDacImpl.cs!TryGetSymbolCore and SOS disasmX86.cpp!HandleValue

            ulong displacement = 0;

            bool TryGetSymbolName(ulong originalAddress, out string name)
            {
                var targetAddress = originalAddress;

                //If the address is for a MethodDesc, the IP needs to be resolved to the MethodDesc it actually pertains to. If this is a jump instruction, the address
                //could be part way into the method. It would seem that ultimately the IP is resolved to a MethodDesc via IJitManager::JitCodeToMethodInfo
                if (sos.TryGetCodeHeaderData(targetAddress, out var codeHeader) == S_OK)
                    targetAddress = codeHeader.MethodDescPtr;

                if (sos.TryGetJitHelperFunctionName(targetAddress, out name) == S_OK)
                    return true;

                if (sos.TryGetMethodTableData(targetAddress, out _) == S_OK)
                    return sos.TryGetMethodTableName(targetAddress, out name) == S_OK;

                if (new DacpMethodDescData().Request(sos.Raw, targetAddress) == S_OK)
                {
                    if (sos.TryGetMethodDescName(targetAddress, out name) == S_OK)
                    {
                        //When the JITType is TYPE_UNKNOWN, the MethodStart will be NULL
                        if (codeHeader.MethodStart != 0)
                            displacement = originalAddress - codeHeader.MethodStart;
                    }

                    return true;
                }

                return false;
            }

            bool TryGetIndirectSymbolName(ulong targetAddress, out string name)
            {
                //When things are byref or are jump thunks and such, we may need to read the memory address in order to
                //get the symbol to resolve. SOS has a bunch of complex heuristics of when to do this in disasm.cpp!GetValueFromExpr and
                //related places, but we'll just try and read anything and see how we go

                if (process.CorDebugProcess.TryReadMemory<IntPtr>(targetAddress, out var newTargetAddress) == S_OK)
                {
                    return TryGetSymbolName((ulong) (void*) newTargetAddress, out name);
                }

                name = null;
                return false;
            }

            if (TryGetSymbolName(address, out var symbolName) || TryGetIndirectSymbolName(address, out symbolName) || TryGetNativeSymbol(address, out symbolName, out displacement))
            {
                //If the symbol is foo+0x100, the symbol foo is -0x100 from the symbol we resolved. Iced will see that the output address
                //is different from the input address and add +0x100 to the resulting output
                symbol = new SymbolResult(address - displacement, symbolName);
                return true;
            }

            symbol = default;
            return false;
        }

        private unsafe bool TryGetNativeSymbol(ulong address, out string name, out ulong displacement)
        {
            if (process.Session.IsInterop)
            {
                //When we're interop debugging, DbgHelp will have all of our native modules in it

                if (process.DbgHelp.TrySymFromAddr((long) address, out var result) == S_OK)
                {
                    name = result.SymbolInfo.ToString();
                    displacement = (ulong) result.Displacement;
                    return true;
                }
            }
            else
            {
                var modules = ProcessModules;

                for (var i = 0; i < modules.Length; i++)
                {
                    var processModule = modules[i].Item1;

                    if (modules[i].Item2 == null)
                        modules[i] = Tuple.Create(processModule, (MODULEINFO?) Kernel32.GetModuleInformation(process.Handle, processModule.BaseAddress));

                    var moduleInfo = modules[i].Item2.Value;

                    var baseAddress = (ulong) (void*) processModule.BaseAddress;
                    var end = baseAddress + (uint) moduleInfo.SizeOfImage;

                    if (address >= baseAddress && address <= end)
                    {
                        name = Path.GetFileNameWithoutExtension(processModule.ModuleName);
                        displacement = address - baseAddress;
                        return true;
                    }
                }
            }

            name = default;
            displacement = default;
            return false;
        }
    }
}
