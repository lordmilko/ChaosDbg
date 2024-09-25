using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ChaosDbg.Analysis;
using ChaosDbg.Disasm;
using ChaosLib;
using ChaosLib.Symbols.MicrosoftPdb;
using ClrDebug.DIA;
using Iced.Intel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    static class IDAComparer
    {
        internal const string IdaString_Subroutine = "; =============== S U B R O U T I N E =======================================";
        internal const string IdaString_StartOfFunctionChunk = "; START OF FUNCTION CHUNK FOR ";
        internal const string IdaString_AdditionalParentFunction = ";   ADDITIONAL PARENT FUNCTION ";

        public static List<(IDAFunctionMetadata ida, INativeFunctionChunkRegion chaos, long[] addresses)> GetMissingIDACode(IDAMetadata[] idaRoutines, PEMetadataPhysicalModule m)
        {
            var idaInstrMap = new Dictionary<long, IDAFunctionMetadata>();

            foreach (var item in idaRoutines.OfType<IDAFunctionMetadata>())
            {
                foreach (var line in item.Code)
                {
                    if (!idaInstrMap.TryGetValue(line.VirtualAddress, out var existing))
                        idaInstrMap[line.VirtualAddress] = item;
                    else
                        throw new NotImplementedException();
                }
            }

            var chaosInstrMap = new Dictionary<long, INativeFunctionChunkRegion>();

            var dupes = m.Ranges.GroupBy(v => v).Where(v => v.Count() > 1).ToArray();

            foreach (var item in m.Ranges.OfType<INativeFunctionChunkRegion>())
            {
                foreach (var instr in item.Instructions)
                {
                    chaosInstrMap.Add(instr.Address, item);
                }
            }

            var missing = new List<KeyValuePair<long, IDAFunctionMetadata>>();

            foreach (var kv in idaInstrMap)
            {
                if (!chaosInstrMap.TryGetValue(kv.Key, out var chaosMetadata))
                    missing.Add(kv);
            }

            var xfg = m.Ranges.OfType<XfgMetadataRange>().ToArray();

            var groups = missing.GroupBy(v => v.Value);

            var bad = new List<(IDAFunctionMetadata ida, INativeFunctionChunkRegion chaos, long[] addresses)>();

            foreach (var group in groups)
            {
                var addresses = group.Select(v => v.Key).ToArray();

                var min = addresses.Min();
                var max = addresses.Max();

                var diff = max - min;

                if (diff < 8)
                {
                    var owner = xfg.Where(x => min >= x.StartAddress && max <= x.EndAddress).ToArray();

                    if (owner.Length > 0)
                        continue;
                }

                var firstInstr = group.Key.Code[0].VirtualAddress;

                chaosInstrMap.TryGetValue(firstInstr, out var chaosFunction);

                bad.Add((group.Key, chaosFunction, addresses.Select(m.GetPhysicalAddress).ToArray()));
            }

            return bad;
        }

        public static void CompareIDAFunctionRanges(IdaContext ida, ChaosContext chaos, PEMetadataPhysicalModule module)
        {
            for (; ida.FunctionIndex < ida.Length; ida.FunctionIndex++, chaos.FunctionIndex++)
            {
                if (ida.CurrentCollapsedFunction != null)
                {
                    chaos.MoveToNextFunction();

                    var name = ((InstructionDiscoverySource) chaos.CurrentRegion.Metadata).Symbol.Name;

                    Assert.AreEqual(name, ida.CurrentCollapsedFunction.Name);
                    var chaosSize = chaos.CurrentRegion.EndAddress - chaos.CurrentRegion.StartAddress + 1;

                    while (chaosSize < ida.CurrentCollapsedFunction.Size)
                    {
                        chaos.FunctionIndex++;
                        chaos.MoveToNextFunction();
                        var extraSize = chaos.CurrentRegion.EndAddress - chaos.CurrentRegion.StartAddress + 1;
                        chaosSize += extraSize;
                    }

                    Assert.AreEqual(chaosSize, ida.CurrentCollapsedFunction.Size);
                    continue;
                }

                if (ida.CurrentFunction == null)
                    continue;

                chaos.MoveToNextFunction();

                var max = Math.Max(chaos.Instructions.Length, ida.CurrentFunction.Code.Length);

                ida.InstructionIndex = 0;
                chaos.InstructionIndex = 0;

                while (true)
                {
                    if (ida.InstructionIndex >= ida.CurrentFunction.Code.Length)
                    {
                        if (chaos.InstructionIndex == chaos.Instructions.Length)
                            break;

                        //After TppWorkerThread, there's an unnamed function that IDA decides is its own function because it was referenced as the end address in a C_SCOPE_TABLE.
                        //I don't know why IDA decided it's its own function, but I think it makes sense for it to be part of TppWorkerThread

                        //Also, for some reason IDA decides that part of RtlLoadString is an unnamed function. I feel like it must be using the unwind info to decide what's a separate
                        //function and what's not

                        if (ida.CurrentFunction.Name.StartsWith("sub") || ida.NextFunction.Name.StartsWith("sub_"))
                        {
                            ida.FunctionIndex++;
                            ida.InstructionIndex = 0;
                        }
                        else
                            throw new NotImplementedException("Don't know how to handle IDA instruction index having exceeded the number of instructions available in the current function at a point where we don't match the number of instructions available in the current ChaosDbg function");
                    }

                    if (chaos.InstructionIndex >= chaos.Instructions.Length)
                    {
                        chaos.FunctionIndex++;
                        chaos.MoveToNextFunction();
                        chaos.InstructionIndex = 0;

                        var rangeStart = module.GetPhysicalAddress(chaos.CurrentRegion.StartAddress);
                    }

                    CompareFunctionNames(ida, chaos);

                    if (ida.CurrentInstruction.VirtualAddress != chaos.CurrentInstruction.Address)
                    {
                        //It's possible for IDA to have erroneously interpreted XFG bytes as disassembly
                        //e.g. the bytes before ntdll!RtlDefaultNpAcl

                        var error = true;

                        //If we're at the start of a new function, and IDA is 8 bytes behind, interpret those bytes as XFG bytes
                        if (chaos.InstructionIndex == 0)
                        {
                            var currentIDAAddr = ida.CurrentInstruction.VirtualAddress;
                            var nextIDAFunction = ida.NextFunction;
                            var idaInstrAfterXFG = nextIDAFunction.Code[0].VirtualAddress;

                            var diff = idaInstrAfterXFG - currentIDAAddr;

                            if (diff == 8)
                            {
                                chaos.FunctionIndex--;
                                break;
                            }
                        }

                        //If we're at the END of a function, and IDA still has 8 more bytes, interpret those as XFG bytes (e.g. TpPoolReferenceExistingGlobalPool)
                        //Some bytes in the XFG may have been interpreted as a string, so we can't check for exactly 8 bytes.
                        if ((ida.NextFunction.Code[0].VirtualAddress - ida.CurrentInstruction.VirtualAddress) <= 8)
                        {
                            chaos.FunctionIndex--;
                            break;
                        }
                        else
                        {
                            //In between a function and an unwind handler, we can have a bunch of junk like this...however we arent catching this above
                            //because we include the unwind handler in the parent function, so we arent "at the end of our current function" yet.
                            //Try and see if there's <= 7 junk bytes prior to the next identifier.
                            var currentIdaIndex = Array.IndexOf(ida.CurrentFunction.Lines, ida.CurrentInstruction);
                            var nextInfo = ida.CurrentFunction.Lines.Skip(currentIdaIndex + 1).SkipWhile(v => !v.IsIdentifier).FirstOrDefault();

                            if (nextInfo != null && nextInfo.IsIdentifier && (nextInfo.VirtualAddress - ida.CurrentInstruction.VirtualAddress) <= 7)
                            {
                                //Get the next code after the identifier
                                var nextInfoIndex = Array.IndexOf(ida.CurrentFunction.Lines, nextInfo);
                                var nextCode = ida.CurrentFunction.Lines.Skip(nextInfoIndex + 1).FirstOrDefault(l => l.Kind == IDALineKind.Code);

                                if (nextCode != null)
                                {
                                    var nextCodeIndex = Array.IndexOf(ida.CurrentFunction.Code, nextCode);
                                    ida.InstructionIndex = nextCodeIndex;
                                }
                            }
                        }

                        //If there was a random cc byte that IDA left as is, but we turned into an int 3, we should skip ahead to the next range
                        if (ida.CurrentInstruction.VirtualAddress == chaos.CurrentInstruction.Address + 1 && chaos.CurrentInstruction.Instruction.Code == Code.Int3)
                        {
                            if (chaos.Instructions.Length == 1)
                            {
                                chaos.FunctionIndex++;
                                chaos.MoveToNextFunction();
                                chaos.InstructionIndex = 0;
                            }
                            else
                            {
                                chaos.InstructionIndex++;

                                if (chaos.InstructionIndex == chaos.Instructions.Length)
                                {
                                    chaos.FunctionIndex++;
                                    chaos.MoveToNextFunction();
                                    chaos.InstructionIndex = 0;
                                }
                            }
                        }

                        //Sometimes you have some garbage 90 (nop) bytes in between chunks. If IDA interpreted these as 90, and we interpreted them as nop's,
                        //skip over these
                        while (chaos.CurrentInstruction.Instruction.Mnemonic == Mnemonic.Nop)
                        {
                            chaos.InstructionIndex++;
                        }

                        //ntdll!strcmp has an odd scenario where theres 8 bytes of junk at the end, followed by a bunch of cc's, followed by the XFG of the next function
                        //These 8 bytes aren't linked to from anywhere
                        if (ida.CurrentInstruction.VirtualAddress != chaos.CurrentInstruction.Address)
                        {
                            var idaCodeIndex = Array.IndexOf(ida.CurrentFunction.Lines, ida.CurrentInstruction);
                            var nextNonCode = ida.CurrentFunction.Lines.Skip(idaCodeIndex).SkipWhile(l => l.Kind == IDALineKind.Code).First();

                            if ((nextNonCode.VirtualAddress + 1 - ida.CurrentInstruction.VirtualAddress) == 8)
                            {
                                //If there's no more code after the next non-code line, then it's trailing junk that we can ignore
                                var nextNonCodeIndex = Array.IndexOf(ida.CurrentFunction.Lines, nextNonCode);

                                if (!ida.CurrentFunction.Lines.Skip(nextNonCodeIndex).Any(v => v.Kind == IDALineKind.Code))
                                {
                                    chaos.FunctionIndex--;
                                    break;
                                }
                            }
                        }

                        var currentChunkStart = (module as PEMetadataVirtualModule)?.GetPhysicalAddress(chaos.CurrentRegion.StartAddress);
                        var previousChaosItems = module.Ranges.Skip(chaos.FunctionIndex - 3).Take(4).ToArray();

                        Assert.AreEqual(ida.CurrentInstruction.VirtualAddress.ToString(), "0x" + chaos.CurrentInstruction.Address.ToString("X"));
                    }

                    ida.InstructionIndex++;
                    chaos.InstructionIndex++;
                }
            }
        }

        private static void CompareFunctionNames(IdaContext ida, ChaosContext chaos)
        {
            if (ida.CurrentInstruction.VirtualAddress != chaos.CurrentInstruction.Address)
                return;

            var idaName = ida.CurrentFunction.Name;
            var chaosFunctionMetadata = (InstructionDiscoverySource) chaos.CurrentRegion.Function.PrimaryMetadata;

            if (chaosFunctionMetadata.Symbol != null)
            {
                var chaosSymbol = (MicrosoftPdbSymbol) chaosFunctionMetadata.Symbol;

                //Functions can be exported with different names than their underlying symbol name
                if (chaosSymbol.Name.TrimStart('_') == idaName.TrimStart('_') || chaosFunctionMetadata.Export?.Name == idaName)
                    return;

                //Compare the decorated symbol names
                if (chaosSymbol.SafeDiaSymbol.Name == idaName)
                    return;

                //There could be duplicates
                if (idaName.EndsWith("_0") || idaName.EndsWith("_1") || idaName.EndsWith("_2"))
                {
                    if (chaosSymbol.Name == idaName.Substring(0, idaName.Length - 2))
                        return;
                }

                //Sometimes the IDA name can be decorated despite the fact that the decorated version isn't present in DIA

                var idaUndecorated = DbgHelp.UnDecorateSymbolName(idaName, UNDNAME.UNDNAME_NAME_ONLY);

                if (chaosSymbol.Name == idaUndecorated)
                    return;

                //We have a good name and they don't, e.g. the code after TppWorkerThread
                if (idaName.StartsWith("sub_"))
                    return;

                if (ida.CurrentFunction.AdditionalParents.Any(p => p.TrimStart('_') == chaosSymbol.Name.TrimStart('_')))
                    return;

                //We have RTL_BINARY_ARRAY<RTLP_FLS_CALLBACK_ENTRY,8,4>::SlotAllocate
                //IDA has RTL_BINARY_ARRAY<RTLP_FLS_CALLBACK_ENTRY,8,4>::SlotAllocate(RTL_BINARY_ARRAY<RTLP_FLS_CALLBACK_ENTRY,8,4> *)

                //The full undecorated name is public: static unsigned long __cdecl RTL_BINARY_ARRAY<struct RTLP_FLS_CALLBACK_ENTRY,8,4>::SlotAllocate(struct RTL_BINARY_ARRAY<struct RTLP_FLS_CALLBACK_ENTRY,8,4> * __ptr64)

                var decorationFlags =
                    UNDNAME.UNDNAME_NO_ACCESS_SPECIFIERS | //public
                    UNDNAME.UNDNAME_NO_MEMBER_TYPE       | //static
                    UNDNAME.UNDNAME_NO_FUNCTION_RETURNS  | //unsigned long
                    UNDNAME.UNDNAME_NO_MS_KEYWORDS       | //__cdecl
                    UNDNAME.UNDNAME_NO_ECSU              | //struct
                    UNDNAME.UNDNAME_NO_PTR64;              //__ptr64

                var chaosUndecorated = ((MicrosoftPdbSymbol) chaosFunctionMetadata.Symbol).SafeDiaSymbol.GetUndecoratedNameEx(decorationFlags);

                if (chaosUndecorated == idaName)
                    return;
            }
            else
            {
                //We know we're at the same instruction
                if (idaName.StartsWith("sub_"))
                    return;
            }

            //IDA treats ___entry_from_strcat_in_strcpy as its own routine. strcpy is defined just prior to it, and I think its basis for doing this is that
            //because strcat jumps to it. DIA says it shouldn't be its own "function" however, so I think IDA's analysis that it is its own "function" is erroneous.
            if (((InstructionDiscoverySource) chaos.CurrentRegion.Metadata)?.Symbol?.Name.TrimStart('_') == idaName.TrimStart('_'))
                return;

            throw new NotImplementedException($"Don't know how to match IDA function {idaName} with ChaosDbg function {chaosFunctionMetadata}");
        }

        public static IDAMetadata[] GetIDARoutines(string path, PEMetadataPhysicalModule module)
        {
            var lines = File.ReadAllLines(path);

            var routineLines = new List<IDALine>();

            var routines = new List<IDAMetadata>();

            void FinalizeRoutine()
            {
                if (routineLines.Count == 0)
                    return;

                var lines = routineLines.ToArray();
                routineLines.Clear();

                if (lines[0].Content == IdaString_Subroutine || lines[0].Content.StartsWith(IdaString_StartOfFunctionChunk))
                    routines.Add(new IDAFunctionMetadata(lines));
                else
                    routines.Add(new IDAUnknownMetadata(lines));
            }

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                var space = line.IndexOf(' ');

                IDALineKind kind = IDALineKind.Unknown;

                if (space == -1)
                    kind = IDALineKind.Empty;
                else
                {
                    var sectionAndAddr = line.Substring(0, space);
                    var content = line.Substring(space + 1);

                    if (content.StartsWith("; Section") && !content.StartsWith("; Section size"))
                    {
                        FinalizeRoutine();
                    }

                    if (content == IdaString_Subroutine || content.StartsWith(IdaString_StartOfFunctionChunk))
                    {
                        FinalizeRoutine();
                    }

                    if (content.Contains("COLLAPSED FUNCTION"))
                    {
                        FinalizeRoutine();

                        var match = Regex.Match(content, "\\[(.+?) BYTES: COLLAPSED FUNCTION (.+?)\\.");

                        if (!match.Success)
                            throw new NotImplementedException();

                        var size = int.Parse(match.Groups[1].Value, NumberStyles.HexNumber);
                        var name = match.Groups[2].Value;

                        routines.Add(new IDACollapsedFunction(name, size));

                        continue;
                    }

                    if (content.StartsWith(";"))
                        kind = IDALineKind.Comment;
                    else
                    {
                        if (content.Length > 1)
                        {
                            if (content[0] != ' ')
                                kind = IDALineKind.Info;
                            else
                            {
                                var word = content.TrimStart();

                                if (word.StartsWith("."))
                                    kind = IDALineKind.Info;
                                else if (word.StartsWith(";"))
                                    kind = IDALineKind.Comment;
                                else if (word.StartsWith("segment") || word.StartsWith("assume") || word.StartsWith("align") || word.StartsWith("public "))
                                    kind = IDALineKind.Info;
                                else if (word.StartsWith("db") || word.StartsWith("dd") || word.StartsWith("dq"))
                                    kind = IDALineKind.Bytes;
                                else
                                    kind = IDALineKind.Code;
                            }
                        }
                    }

                    routineLines.Add(new IDALine(sectionAndAddr, content, kind, module));
                }
            }

            FinalizeRoutine();

            return routines.ToArray();
        }

    }
}
