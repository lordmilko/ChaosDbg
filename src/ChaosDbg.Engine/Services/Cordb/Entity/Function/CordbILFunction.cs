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
        public CorDebugFunction CorDebugFunction { get; }

        public MdiMethodDef MetaData { get; }

        public CordbILFrame Frame { get; }

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
                    var process = Frame.Module.Process;
                    var nativeDisasmProvider = process.Session.Services.NativeDisasmProvider;

                    var nativeCode = CorDebugFunction.NativeCode;

                    //Code can be split into multiple chunks. While it's easy enough to iterate over those chunks and read the memory
                    //into a big byte array, when we actually perform the disassembly we'll need to factor in the actual address at which
                    //each instruction resides. As such, for now we throw if we see code that extends over more than one chunk
                    var codeChunks = nativeCode.CodeChunks;

                    if (codeChunks.Length > 1)
                        throw new NotImplementedException("Disassembling native code split across multiple code chunks is not implemented. A custom stream is needed so that the code in each chunk has its IP set correctly during disassembly.");

                    var codeChunk = codeChunks.Single();

                    var nativeBytes = process.CorDebugProcess.ReadMemory(codeChunk.startAddr, codeChunk.length);

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
                    var process = Frame.Module.Process;
                    var ilDisasmProvider = process.Session.Services.ILDisasmProvider;

                    var ilDisassembler = ilDisasmProvider.CreateDisassembler(CorDebugFunction, Frame.Module);

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

                    for (var i = 0; i < ilToNativeMappingNativeSort.Length; i++)
                    {
                        var item = ilToNativeMappingNativeSort[i];

                        var instrs = GetNativeInstructionsInRange(nativeInstructions, item.nativeStartOffset, item.nativeEndOffset);

                        //Certain items represent special code regions, and have negative numbers instead of proper offsets
                        switch ((CorDebugIlToNativeMappingTypes) item.ilOffset)
                        {
                            case CorDebugIlToNativeMappingTypes.NO_MAPPING:
                                results[i] = new ILToNativeInstruction(ILToNativeInstructionKind.Unknown, null, instrs, item);
                                break;

                            case CorDebugIlToNativeMappingTypes.PROLOG:
                                results[i] = new ILToNativeInstruction(ILToNativeInstructionKind.Prolog, null, instrs, item);
                                break;

                            case CorDebugIlToNativeMappingTypes.EPILOG:
                                results[i] = new ILToNativeInstruction(ILToNativeInstructionKind.Epilog, null, instrs, item);
                                break;

                            default:
                                //It's a normal code region

                                var ilInstrs = GetILInRange(ilInstructions, ilToNativeMappingILSort, item);

                                results[i] = new ILToNativeInstruction(ILToNativeInstructionKind.Code, ilInstrs, instrs, item);
                                break;
                        }
                    }

#if DEBUG
                    var missingIL = ilInstructions.Except(results.SelectMany(r => r.IL ?? Array.Empty<ILInstruction>())).ToArray();
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
            COR_DEBUG_IL_TO_NATIVE_MAP item)
        {
            var i = 0;

            var currentOffset = 0;

            //Skip ahead to find the first instruction to start from

            for (; i < ilInstructions.Length; i++)
            {
                if (currentOffset == item.ilOffset)
                    break;

                if (currentOffset > item.ilOffset)
                    throw new NotImplementedException($"{nameof(currentOffset)} ({currentOffset}) was greater than starting offset {item.ilOffset}. This should be impossible");

                currentOffset += ilInstructions[i].Length;
            }

            //While we aren't told how large we are, I suppose we can say that we run up until the next IL item

            var currentIndex = Array.IndexOf(ilToNativeMappingILSort, item);

            var results = new List<ILInstruction>();

            if (currentIndex == ilToNativeMappingILSort.Length - 1)
            {
                //We're the last one. This shouldn't be possible (since we expect to have epilog after us)

                for (; i < ilInstructions.Length; i++)
                    results.Add(ilInstructions[i]);
            }
            else
            {
                var nextItem = ilToNativeMappingILSort[currentIndex + 1];

                if (nextItem.ilOffset < 0)
                {
                    for (var j = currentIndex + 2; j < ilToNativeMappingILSort.Length; j++)
                    {
                        //We've got a big problem. Our entire logic of knowing how big a given string of IL instructions is is predicated
                        //on knowing the offset of the next instruction. But if theres a special instruction up next, followed by more normal instructions,
                        //our logic breaks down
                        if (ilToNativeMappingILSort[j].ilOffset > 0)
                            throw new NotImplementedException("Encountered an IL to native mapping with a normal offset after we expected that there should be no more normal mappings");
                    }
                }

                //Eat instructions until we come to the next item

                if (item.ilOffset == nextItem.ilOffset)
                {
                    //A single ilOffset can sometimes result in two separate batches of native code being generated
                    results.Add(ilInstructions[i]);
                }
                else
                {
                    for (; i < ilInstructions.Length; i++)
                    {
                        //This logic is predicated on there never being more normal instructions after special instructions after us
                        if (nextItem.ilOffset > 0 && currentOffset >= nextItem.ilOffset)
                            break;

                        var currentInstr = ilInstructions[i];

                        results.Add(currentInstr);

                        currentOffset += currentInstr.Length;
                    }
                }
            }

            return results.ToArray();
        }

        #endregion

        internal CordbILFunction(CordbILFrame frame)
        {
            Frame = frame;
            CorDebugFunction = frame.CorDebugFrame.Function;
            MetaData = frame.Module.MetaDataProvider.ResolveMethodDef(CorDebugFunction.Token);
        }

        public override string ToString() => MetaData.ToString();
    }
}
