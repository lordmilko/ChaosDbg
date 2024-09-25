using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ChaosDbg.Disasm;
using ChaosDbg.IL;
using ChaosLib.Metadata;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    public class CordbILFunction
    {
        #region Address

        private CLRDATA_ADDRESS? clrAddress;

        public CLRDATA_ADDRESS ClrAddress
        {
            get
            {
                if (clrAddress == null)
                {
                    var sos = Module.Process.DAC.SOS;

                    var rawAddress = sos.GetMethodDescFromToken(Module.ClrAddress, CorDebugFunction.Token);

                    //If we get an address of 0 I think this may mean that the function isn't jitted yet
                    if (rawAddress != 0)
                        clrAddress = rawAddress;
                    else
                        return 0;
                }

                return clrAddress.Value;
            }
        }

        #endregion

        public CorDebugFunction CorDebugFunction { get; }

        private IMetadataMethodBase metadataMethod;

        private IMetadataMethodBase MetadataMethod
        {
            get
            {
                if (metadataMethod == null)
                    metadataMethod = (IMetadataMethodBase) Module.MetadataModule.ResolveMethod(CorDebugFunction.Token);

                return metadataMethod;
            }
        }

        public CordbManagedModule Module { get; }

        private INativeInstruction[] disassembly;

        /// <summary>
        /// Gets all native instructions associated with this function.
        /// </summary>
        public INativeInstruction[] Disassembly
        {
            get
            {
                if (disassembly == null)
                {
                    var process = Module.Process;
                    var nativeDisasmProvider = process.Session.Services.NativeDisasmProvider;

                    var nativeCode = CorDebugFunction.NativeCode;

                    //Code can be split into multiple chunks. While it's easy enough to iterate over those chunks and read the memory
                    //into a big byte array, when we actually perform the disassembly we'll need to factor in the actual address at which
                    //each instruction resides. As such, for now we throw if we see code that extends over more than one chunk
                    var codeChunks = nativeCode.CodeChunks;

                    if (codeChunks.Length > 1)
                        throw new NotImplementedException("Disassembling native code split across multiple code chunks is not implemented. A custom stream is needed so that the code in each chunk has its IP set correctly during disassembly. Or, maybe we can just loop over the chunks and update the IP of the disassembler prior to the start of each one? Need to verify whatever we do actually works");

                    var codeChunk = codeChunks.Single();

                    var nativeBytes = process.ReadManagedMemory(codeChunk.startAddr, codeChunk.length);

                    //Note that when we implement support for having multiple chunks, we'll likely need some kind of intelligent stream
                    //that updates the current Position to be the start of the next chunk when the bytes of the current chunk have been exhausted
                    var nativeDisassembler = nativeDisasmProvider.CreateDisassembler(nativeBytes, codeChunk.startAddr, process.Is32Bit, new CordbDisasmSymbolResolver(process));

                    //Decode all of the bytes that comprise the function into instructions
                    disassembly = nativeDisassembler.EnumerateInstructions().ToArray();
                }

                return disassembly;
            }
        }

        private ILInstruction[] il;

        public ILInstruction[] IL
        {
            get
            {
                if (il == null)
                {
                    var ilDisassembler = ILDisassembler.Create(CorDebugFunction, Module);

                    il = ilDisassembler.EnumerateInstructions().ToArray();
                }

                return il;
            }
        }

        #region ILToDisassembly

        private ILToNativeInstruction[] ilToDisassembly;

        public ILToNativeInstruction[] ILToDisassembly
        {
            get
            {
                if (ilToDisassembly == null)
                {
                    var nativeInstructions = Disassembly;
                    var ilInstructions = IL;

                    var nativeCode = CorDebugFunction.NativeCode;

                    //By default, ILToNativeMapping is sorted by ilOffset
                    var ilToNativeMappingILSort = nativeCode.ILToNativeMapping.ToArray();
                    var ilToNativeMappingNativeSort = ilToNativeMappingILSort.OrderBy(v => v.nativeStartOffset).ToArray();

                    //Check whether the end of each region corresponds with the start of the next region.
                    //If this is ever not the case, there's something extra that we don't have data for

                    for (var i = 0; i < ilToNativeMappingNativeSort.Length - 1; i++)
                    {
                        if (ilToNativeMappingNativeSort[i].nativeEndOffset != ilToNativeMappingNativeSort[i + 1].nativeStartOffset)
                            throw new NotImplementedException("Don't know how to handle a non-contiguous IL to Native mapping list");
                    }

                    var results = new ILToNativeInstruction[ilToNativeMappingILSort.Length];

                    var ilIndex = 0;

                    for (var i = 0; i < ilToNativeMappingNativeSort.Length; i++)
                    {
                        var item = ilToNativeMappingILSort[i];
                        var ilInstrs = GetILInRange(ilInstructions, ilToNativeMappingILSort, i, ref ilIndex);
                        var instrs = GetNativeInstructionsInRange(nativeInstructions, item.nativeStartOffset, item.nativeEndOffset);

                        //Certain items represent special code regions, and have negative numbers instead of proper offsets
                        switch ((CorDebugIlToNativeMappingTypes) item.ilOffset)
                        {
                            case CorDebugIlToNativeMappingTypes.NO_MAPPING:
                                results[i] = new ILToNativeInstruction(ILToNativeInstructionKind.NoMapping, ilInstrs, instrs, item);
                                break;

                            case CorDebugIlToNativeMappingTypes.PROLOG:
                                results[i] = new ILToNativeInstruction(ILToNativeInstructionKind.Prolog, ilInstrs, instrs, item);
                                break;

                            case CorDebugIlToNativeMappingTypes.EPILOG:
                                results[i] = new ILToNativeInstruction(ILToNativeInstructionKind.Epilog, ilInstrs, instrs, item);
                                break;

                            default:
                                //It's a normal code region
                                results[i] = new ILToNativeInstruction(ILToNativeInstructionKind.Code, ilInstrs, instrs, item);
                                break;
                        }
                    }

#if DEBUG
                    //Assert that all native/IL instructions were matched against

                    var missingIL = ilInstructions.Except(results.SelectMany(r => r.IL)).ToArray();
                    var missingNative = nativeInstructions.Except(results.SelectMany(r => r.NativeInstructions)).ToArray();

                    Debug.Assert(missingIL.Length == 0, "Had extra IL instructions that were not assigned to a mapping");
                    Debug.Assert(missingNative.Length == 0, "Had extra native instructions that were not assigned to a mapping");
#endif
                    ilToDisassembly = results;
                }

                return ilToDisassembly;
            }
        }

        private static INativeInstruction[] GetNativeInstructionsInRange(INativeInstruction[] nativeInstructions, int startOffset, int endOffset)
        {
            var i = 0;

            var currentOffset = 0;

            //Skip ahead to find the first instruction to start from

            for (; i < nativeInstructions.Length; i++)
            {
                if (currentOffset == startOffset)
                    break;

                if (currentOffset > startOffset)
                    throw new NotImplementedException();

                currentOffset += nativeInstructions[i].Bytes.Length;
            }

            var matchingInstrs = new List<INativeInstruction>();

            //Read instructions until we're past the desired offset

            for (; i < nativeInstructions.Length; i++)
            {
                if (currentOffset >= endOffset)
                    break;

                var currentInstr = nativeInstructions[i];

                matchingInstrs.Add(currentInstr);

                currentOffset += currentInstr.Bytes.Length;
            }

            return matchingInstrs.ToArray();
        }

        private static ILInstruction[] GetILInRange(
            ILInstruction[] ilInstructions,
            COR_DEBUG_IL_TO_NATIVE_MAP[] ilToNativeMappingILSort,
            int mappingIndex,
            ref int ilIndex)
        {
            if (mappingIndex < ilToNativeMappingILSort.Length - 1)
            {
                /* We can sometimes have a prolog mapping (IL Offset -2) followed by a code mapping with an offset above 0 (e.g. 8).
                 * In this instance, the native bytes covered by the prolog region also consumed some of our IL instructions. We know
                 * this is true, because I saw a prolog section that covered 30 native bytes, containing a call that was made in the first
                 * 3 IL instructions. The first code region at IL Offset 8, thereby proving that you can have IL as part of the prolog region */
                var startOffset = Math.Max(0, ilToNativeMappingILSort[mappingIndex].ilOffset);
                var endOffset = ilToNativeMappingILSort[mappingIndex + 1].ilOffset;

                if (endOffset < 0)
                {
                    //Either there are multiple prolog regions, or we're at the epilog. If there's another code region after us, do nothing
                    if (ilToNativeMappingILSort.Skip(mappingIndex + 1).Any(v => v.ilOffset >= 0))
                        return Array.Empty<ILInstruction>();

                    //There's an epilog after us. Consume all remaining instructions
                    var remaining = ilInstructions.Length - ilIndex;

                    var results = new ILInstruction[remaining];

                    for (var i = 0; ilIndex < ilInstructions.Length; i++, ilIndex++)
                        results[i] = ilInstructions[ilIndex];

                    return results;
                }
                else
                {
                    var currentOffset = startOffset;

                    var results = new List<ILInstruction>();

                    while (currentOffset < endOffset && ilIndex < ilInstructions.Length)
                    {
                        var instr = ilInstructions[ilIndex];
                        results.Add(instr);
                        ilIndex++;
                        currentOffset += instr.Length;
                    }

                    return results.ToArray();
                }
            }
            else
            {
                if (ilIndex == ilInstructions.Length)
                    return Array.Empty<ILInstruction>();

                //There isn't always an epilog.

                var ilOffset = ilToNativeMappingILSort[mappingIndex].ilOffset;
                var remaining = ilInstructions.Length - ilIndex;

                if (ilOffset < 0)
                {
                    //It's a special region (like an epilog). The fact there's still IL instructions we haven't claimed indicates a bug with our logic above for the regions
                    //that came before us

                    throw new InvalidOperationException($"Had {remaining} unclaimed IL instructions when the last mapping region (with offset {ilOffset}) was reached. This indicates a bug.");
                }

                if (ilIndex < ilInstructions.Length)
                {
                    //We're a normal code region (and there is no epilog). All remaining instructions belong to us!

                    var results = new ILInstruction[remaining];

                    for (var i = 0; ilIndex < ilInstructions.Length; i++, ilIndex++)
                        results[i] = ilInstructions[ilIndex];

                    return results;
                }

                throw new NotImplementedException($"IL index {ilIndex} should not be greater than the number of instructions available ({ilInstructions.Length})");
            }
        }

        #endregion

        /// <summary>
        /// Gets the JIT status of this function.
        /// </summary>
        public JITTypes JITStatus
        {
            get
            {
                var address = ClrAddress;

                if (address == 0)
                {
                    //We can't ask SOS because I think the method isnt jitted yet

                    //CordbFunction::GetNativeCode says that CORDBG_E_CODE_NOT_AVAILABLE means the method
                    //isn't jitted
                    if (CorDebugFunction.TryGetNativeCode(out _) == HRESULT.CORDBG_E_CODE_NOT_AVAILABLE)
                        return JITTypes.TYPE_UNKNOWN;
                }

                var sos = Module.Process.DAC.SOS;

                if (sos.TryGetCodeHeaderData(address, out var data) == HRESULT.S_OK)
                {
                    if (data.JITType == JITTypes.TYPE_UNKNOWN)
                    {
                        //Try get more accurate information using the native code
                        DacpMethodDescData methodDescData = new DacpMethodDescData();

                        if (methodDescData.Request(sos.Raw, address) == HRESULT.S_OK)
                        {
                            if (sos.TryGetCodeHeaderData(methodDescData.NativeCodeAddr, out data) == HRESULT.S_OK)
                                return data.JITType;
                        }

                        return JITTypes.TYPE_UNKNOWN;
                    }

                    return data.JITType;
                }
                else
                {
                    //Try get more accurate information using the native code
                    DacpMethodDescData methodDescData = new DacpMethodDescData();

                    if (methodDescData.Request(sos.Raw, address) == HRESULT.S_OK)
                    {
                        if (sos.TryGetCodeHeaderData(methodDescData.NativeCodeAddr, out data) == HRESULT.S_OK)
                            return data.JITType;
                    }
                }

                if (CorDebugFunction.TryGetNativeCode(out _) == HRESULT.CORDBG_E_CODE_NOT_AVAILABLE)
                    return JITTypes.TYPE_UNKNOWN;

                throw new NotImplementedException("Don't know how to figure out what the JIT status is");
            }
        }

        internal CordbILFunction(CorDebugFunction corDebugFunction, CordbManagedModule module)
        {
            CorDebugFunction = corDebugFunction;
            Module = module;
        }

        public override string ToString() => MetadataMethod.ToString();
    }
}
