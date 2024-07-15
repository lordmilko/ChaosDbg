using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ChaosDbg.Analysis;
using ChaosDbg.Disasm;
using ChaosDbg.SymStore;
using ChaosLib;
using ChaosLib.Memory;
using ChaosLib.PortableExecutable;
using ChaosLib.Symbols;
using Iced.Intel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    [TestClass]
    public class PEModuleMetadataTests : BaseTest
    {
        #region NoReturn

        [TestMethod]
        public void PEModuleMetadata_NoReturn_RtlRaiseStatus()
        {
            //RtlRaiseStatus calls back into itself

            TestMatchAllFunctions(
                WellKnownTestModule.Ntdll,
                PEMetadataSearchOptions.Symbols,
                (s, _) => new MockSymbolModule(s, "RtlRaiseStatus", "LdrpReportError"),
                m =>
                {
                    var function = GetRegion(m, "RtlRaiseStatus");

                    Assert.IsFalse(function.Function.PrimaryMetadata.DoesReturn);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_NoReturn_RtlCallEnclave()
        {
            //RtlCallEnclave DOES return, but it ends in a call instruction prior to something with another symbol

            TestSymbolsAndExports(
                new[] { "LdrpIssueEnclaveCall", "RtlCallEnclave", "RtlCallEnclaveReturn" },
                m =>
                {
                    var regions = GetFunctionsWithSymbols(m).DistinctBy(v => v.Metadata).ToArray();

                    Assert.AreEqual(4, regions.Length);

                    Assert.AreEqual("RtlCallEnclave", regions[0].Metadata.Symbol.Name);
                    Assert.AreEqual("RtlCallEnclaveReturn", regions[1].Metadata.Symbol.Name);
                    Assert.AreEqual("LdrpIssueEnclaveCall", regions[2].Metadata.Symbol.Name);
                    Assert.IsNull(regions[3].Metadata); //Part of LdrpIssueEnclaveCall

                    //Verify that RtlCallEnclave and RtlCallEnclaveReturn are part of the same function. This is true only when RtlCallEnclaveReturn has an export
                    Assert.AreEqual(regions[0].Function, regions[1].Function);

                    //Verify that LdrpIssueEnclaveCall and the other region are part of the same function
                    Assert.AreEqual(regions[2].Function, regions[3].Function);

                    //Verify that RtlCallEnclave is immediately followed by RtlCallEnclaveReturn
                    Assert.AreEqual(regions[0].EndAddress + 1, regions[1].StartAddress);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_NoReturn_DbgUiRemoteBreakin()
        {
            //DbgUiRemoteBreakin should be noreturn because it ends by calling RtlExitUserThread which is no return

            TestMatchAllFunctions(
                WellKnownTestModule.Ntdll,
                PEMetadataSearchOptions.Symbols,
                (s, _) => new MockSymbolModule(s, "DbgUiRemoteBreakin"),
                m =>
                {
                    var function = GetRegion(m, "DbgUiRemoteBreakin");

                    Assert.AreEqual(1, function.Function.Chunks.Count);
                    Assert.AreEqual(Mnemonic.Call, function.Function.AllInstructions.Last().Instruction.Mnemonic);
                }
            );
        }

        #endregion
        #region JumpTable

        [TestMethod]
        public void PEModuleMetadata_JumpTable_RtlMapSecurityErrorToNtStatus()
        {
            //RtlMapSecurityErrorToNtStatus

            TestSymbolsAndUnwindData(
                new[] { "RtlMapSecurityErrorToNtStatus" },
                m =>
                {
                    var jumpTable = m.Ranges.OfType<JumpTableMetadataRange>().Single();
                    var region = GetRegion(m, "RtlMapSecurityErrorToNtStatus");

                    VerifyJumpTable(m, region, jumpTable);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_JumpTable_RtlpUnwindPrologue()
        {
            //RtlpUnwindPrologue
            //r15 is set in a prior chunk

            TestSymbolsAndUnwindData(
                new[] { "RtlpUnwindPrologue", "__C_specific_handler" },
                m =>
                {
                    var jumpTable = m.Ranges.OfType<JumpTableMetadataRange>().Single();
                    var region = GetRegion(m, "RtlpUnwindPrologue");

                    VerifyJumpTable(m, region, jumpTable);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_JumpTable_LdrpSearchResourceSection_U()
        {
            TestSymbolsAndUnwindData(
                new[] { "LdrpSearchResourceSection_U" },
                m =>
                {
                    var region = GetRegion(m, "LdrpSearchResourceSection_U");
                    var jumpTable = m.Ranges.OfType<JumpTableMetadataRange>().Single(j => j.FunctionAddress == region.Function.Address);

                    VerifyJumpTable(m, region, jumpTable);
                }
            );
        }

        #endregion

        [TestMethod]
        public void PEModuleMetadata_InstructionsBetweenChunks_KiUserApcDispatcher()
        {
            //KiUserApcDispatcher calls RtlRaiseStatus, and then has a jmp back to the start of the block where that call is made. Because RtlRaiseStatus is known
            //not to return, we never disassemble the jmp instruction. But because both the previous and next chunks are part of the same function, we need to detect
            //that it is valid to disassemble all the instructions in-between.
            TestMatchAllFunctions(
                WellKnownTestModule.Ntdll,
                PEMetadataSearchOptions.Symbols,
                (s, _) => new MockSymbolModule(s, "KiUserApcDispatcher"),
                m =>
                {
                    //Verify we have the jmp after the call to RtlRaiseStatus
                    var regions = GetFunctionRegions(m, "KiUserApcDispatcher");
                    Assert.AreEqual(1, regions.Length);

                    var rtlRaiseStatus = regions[0].Instructions.FindLastIndex(i => i.Instruction.Mnemonic == Mnemonic.Call && i.ToString().Contains("RtlRaiseStatus"));

                    var afterCall = regions[0].Instructions.Skip(rtlRaiseStatus + 1).ToArray();
                    Assert.AreEqual(12, afterCall.Length);
                    Assert.AreEqual(Mnemonic.Jmp, afterCall[0].Instruction.Mnemonic);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_InstructionsBetweenChunks_KiUserExceptionDispatcher()
        {
            //KiUserExceptionDispatcher also has the same thing - it calls RtlRaiseStatus, but then theres a nop and a ret at the end
            TestMatchAllFunctions(
                WellKnownTestModule.Ntdll,
                PEMetadataSearchOptions.Symbols,
                (s, _) => new MockSymbolModule(s, "KiUserExceptionDispatcher"),
                m =>
                {
                    //Verify we have the jmp after the call to RtlRaiseStatus
                    var regions = GetFunctionRegions(m, "KiUserExceptionDispatcher");
                    Assert.AreEqual(1, regions.Length);

                    var rtlRaiseStatus = regions[0].Instructions.FindLastIndex(i => i.Instruction.Mnemonic == Mnemonic.Call);

                    var afterCall = regions[0].Instructions.Skip(rtlRaiseStatus + 1).ToArray();

                    Assert.AreEqual(2, afterCall.Length);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_OverlappingSymbols_SymbolOwnedByOneFunctionUsedByAnother_entry_from_strcat_in_strcpy()
        {
            //___entry_from_strcat_in_strcpy is defined directly under strcpy, however strcat contains conditional jumps that go
            //straight into ___entry_from_strcat_in_strcpy

            TestMatchAllFunctions(
                WellKnownTestModule.Ntdll,
                PEMetadataSearchOptions.Symbols,
                (s, _) => new MockSymbolModule(s, "strcpy", "strcat", "___entry_from_strcat_in_strcpy"),
                m =>
                {
                    var regions = GetFunctionsWithSymbols(m);

                    //___entry_from_strcat_in_strcpy shouldn't be its own function. I think IDA treats it as one because there's
                    //a jump to it from strcat. But DIA says it isn't its own function, and this makes sense because strcpy only
                    //has a single instruction in it

                    Assert.AreEqual(3, regions.Length);

                    Assert.AreEqual("strcat", regions[0].Function.PrimaryMetadata.Symbol.Name);
                    Assert.AreEqual("strcpy", regions[1].Function.PrimaryMetadata.Symbol.Name);
                    Assert.AreEqual("strcpy", regions[2].Function.PrimaryMetadata.Symbol.Name);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_OverlappingSymbols_KiUserCallbackDispatcher()
        {
            TestSymbolsAndUnwindData(
                new[] { "KiUserCallbackDispatcher", "KiUserCallbackDispatcherHandler", "KiUserCallbackDispatcherContinue" },
                m =>
                {
                    //KiUserCallbackDispatcher contains KiUserCallbackDispatcherContinue

                    var regions = GetFunctionsWithSymbols(m).DistinctBy(v => v.Metadata).ToArray();
                    Assert.AreEqual(3, regions.Length);
                    Assert.AreEqual("KiUserCallbackDispatcherHandler", regions[0].Metadata.Symbol.Name);
                    Assert.AreEqual("KiUserCallbackDispatcher", regions[1].Metadata.Symbol.Name);
                    Assert.AreEqual("KiUserCallbackDispatcherContinue", regions[2].Metadata.Symbol.Name);

                    var distinctFunctions = regions.DistinctBy(r => r.Function.PrimaryMetadata).ToArray();
                    Assert.AreEqual(2, distinctFunctions.Length);
                    Assert.AreEqual("KiUserCallbackDispatcherHandler", distinctFunctions[0].Metadata.Symbol.Name);
                    Assert.AreEqual("KiUserCallbackDispatcher", distinctFunctions[1].Metadata.Symbol.Name);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_Issue_TppPoolpFree_WithAllReferences()
        {
            /*
     TppPoolpFree               TppPoolpFree$fin$0
     |           |                      |
 18008528B    180085292      RtlReleaseSRWLockExclusive
                                        |
                                    1800336DE
                                        |
                                 RtlpWakeSRWLock
                                  |           |
                              18003124B    1800BAE7A
                                  |
                              180031252
                                  |
                              180031262
                                  |
                              18003127D
             */
            TestSymbolsAndUnwindData(
                new[] { "TppPoolpFree", "TppPoolpFree$fin$0", "RtlpWakeSRWLock", "RtlReleaseSRWLockExclusive", "__C_specific_handler" },
                m =>
                {
                    var regions = GetFunctionsWithSymbols(m);

                    var groups = regions.GroupBy(f => f.Function.PrimaryMetadata).ToArray();

                    Assert.AreEqual(4, groups.Length);
                    Assert.AreEqual("RtlpWakeSRWLock", groups[0].Key.Symbol.Name);
                    Assert.AreEqual("RtlReleaseSRWLockExclusive", groups[1].Key.Symbol.Name);
                    Assert.AreEqual("TppPoolpFree", groups[2].Key.Symbol.Name);
                    Assert.AreEqual("_C_specific_handler", groups[3].Key.Symbol.Name);
                    Assert.IsTrue(groups[2].Any(v => v.Metadata?.Symbol?.Name == "TppPoolpFree$fin$0"));
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_Issue_TppPoolpFree_WithoutRtlReleaseSRWLockExclusive()
        {
            /*
     TppPoolpFree               TppPoolpFree$fin$0 (1800A98D8)
     |           |                      |
 18008528B    180085292       1800336D0 (RtlReleaseSRWLockExclusive)
                                        |
                                    1800336DE
                                        |
                                 RtlpWakeSRWLock
                                  |           |
                              18003124B    1800BAE7A
                                  |
                              180031252
                                  |
                              180031262
                                  |
                              18003127D
             */
            TestSymbolsAndUnwindData(
                new[] { "TppPoolpFree", "TppPoolpFree$fin$0", "RtlpWakeSRWLock", "__C_specific_handler" },
                m =>
                {
                    var regions = GetFunctionsWithSymbols(m);

                    var groups = regions.GroupBy(f => f.Function.PrimaryMetadata).ToArray();

                    Assert.AreEqual(3, groups.Length);
                    Assert.AreEqual("RtlpWakeSRWLock", groups[0].Key.Symbol.Name);
                    Assert.AreEqual("TppPoolpFree", groups[1].Key.Symbol.Name);
                    Assert.AreEqual("_C_specific_handler", groups[2].Key.Symbol.Name);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_OverlappingSymbols_OneChunkContainsAllOthers()
        {
            TestSymbolsAndUnwindData(
                new[] { "RtlFreeHeap" },
                m =>
                {
                    var region = GetRegion(m, "RtlFreeHeap");
                    Assert.AreEqual(4, region.Function.Chunks.Count);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_OverlappingSymbols_OneFunctionContainsAllOthers()
        {
            TestSymbolsAndUnwindData(
                new[] { "RtlSetProtectedPolicy" },
                m =>
                {
                    var region = GetRegion(m, "RtlSetProtectedPolicy");
                    Assert.AreEqual(3, region.Function.Chunks.Count);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_Issue_RtlExpandEnvironmentStrings()
        {
            TestSymbolsAndUnwindData(
                new[] { "RtlExpandEnvironmentStrings" },
                m =>
                {
                    var region = GetRegion(m, "RtlExpandEnvironmentStrings");
                    Assert.AreEqual(3, region.Function.Chunks.Count);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_Issue_RtlpAllocateHeap()
        {
            TestMatchAllFunctions(
                WellKnownTestModule.Ntdll,
                PEMetadataSearchOptions.Symbols,
                (s, _) => new MockSymbolModule(s, "RtlpAllocateHeap", "RtlpHpLfhSlotAllocate"),
                m =>
                {
                    //RtlpHpLfhSlotAllocate has a chunk that ends with an int 29, immediately followed by a chunk that belongs to RtlpAllocateHeap.
                    //This chunk erroneously gets merged into RtlpHpLfhSlotAllocate

                    var regions = GetFunctionsWithSymbols(m).GroupBy(f => f.Function.PrimaryMetadata).ToArray();

                    Assert.AreEqual(2, regions.Length);

                    //Assert that RtlpHpLfhSlotAllocate has a region ending in int 29, proving that it didn't erroneously absorb the instructions
                    //belonging to RtlpAllocateHeap

                    Assert.AreEqual("RtlpHpLfhSlotAllocate", regions[0].Key.Symbol.Name);
                    Assert.AreEqual("RtlpAllocateHeap", regions[1].Key.Symbol.Name);

                    Assert.IsTrue(regions[0].Any(r =>
                    {
                        var lastInstr = r.Instructions.Last();

                        return lastInstr.Instruction.FlowControl == FlowControl.Interrupt && lastInstr.Instruction.Immediate8 == WellKnownInterrupt.FailFast;
                    }));
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_Issue_RtlLookupFunctionEntry()
        {
            TestSymbolsAndUnwindData(
                new[] { "RtlLookupFunctionEntry" },
                m =>
                {
                    var chunk = GetRegion(m, "RtlLookupFunctionEntry");

                    //Assert that the cc instruction that is pointed to by unwind data and followed by cmp     byte ptr [r8+7], 0
                    //is included in the definition of the function. Looks like it works in this isolated test, but it doesn't work in the full ntdll test

                    Assert.AreEqual(5, chunk.Function.Chunks.Count);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_Issue_LdrpFindLoadedDllByName()
        {
            TestSymbolsAndUnwindData(
                new[] { "LdrpFindLoadedDllByName" },
                m =>
                {
                    var region = GetRegion(m, "LdrpFindLoadedDllByName");
                    Assert.AreEqual(3, region.Function.Chunks.Count);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_Issue_RtlGetCurrentProcessorNumberEx()
        {
            //These functions jump between each other, and as a result all the chunks from RtlGetCurrentProcessorNumberEx get deleted
            //and then there's also a jump to NtGetCurrentProcessorNumberEx at the end too

            TestSymbolsAndUnwindData(
                new[] { "RtlGetCurrentProcessorNumberEx", "RtlpGetCurrentProcessorNumberExUninitialized", "NtGetCurrentProcessorNumberEx" },
                m =>
                {
                    var regions = m.Ranges.OfType<NativeFunctionChunkRegion<InstructionDiscoverySource>>().Select(v => v.Function.PrimaryMetadata).Where(v => v != null).Distinct().ToArray();

                    Assert.AreEqual(3, regions.Length);
                    Assert.AreEqual("RtlpGetCurrentProcessorNumberExUninitialized", regions[0].Symbol.Name);
                    Assert.AreEqual("NtGetCurrentProcessorNumberEx", regions[1].Symbol.Name);
                    Assert.AreEqual("RtlGetCurrentProcessorNumberEx", regions[2].Symbol.Name);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_Issue_RtlpHpVsSubsegmentCleanup()
        {
            //These share their final block

            TestSymbolsAndUnwindData(
                new[] { "RtlpHpVsSubsegmentCleanup", "RtlpHpSegHeapAddSegment" },
                m =>
                {
                    var regions = GetFunctionsWithSymbols(m).GroupBy(f => f.Function.PrimaryMetadata).ToArray();

                    Assert.AreEqual(2, regions.Length);
                    Assert.AreEqual("RtlpHpSegHeapAddSegment", regions[0].Key.Symbol.Name);
                    Assert.AreEqual(1, regions[0].Count());

                    Assert.AreEqual("RtlpHpVsSubsegmentCleanup", regions[1].Key.Symbol.Name);
                    Assert.AreEqual(2, regions[1].Count());
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_Issue_UnreferencedRoots()
        {
            /*
             * ntdll!RtlpTpWaitFinalizationCallback                    ntdll!RtlpFreeDebugInfo
             *               |                                      /                            \
             *           18004FC49                               18005A9C9                ntdll!RtlInterlockedPushEntrySList
             *               |                                    |     |                        |
             *            18004F5FC (RtlpTpWaitRundown)           |   8005A9EC           1800A3130 (RtlpInterlockedPushEntrySList)
             *          /                 |              \        |
             *    18004F632           18004F63B             ntdll!RtlFreeHeap
             *                                             /         |        \           \
             *                                       18003AB28   18003AB2F  18003AB45   1800BC84C
             *                                                                 |
             *                                                              1800BC871
             */

            TestSymbolsAndUnwindData(
                new[] { "RtlFreeHeap", "RtlInterlockedPushEntrySList", "RtlpFreeDebugInfo", "RtlpTpWaitFinalizationCallback" },
                m =>
                {
                    var regions = GetFunctionsWithSymbols(m).GroupBy(v => v.Function.PrimaryMetadata).ToArray();

                    Assert.AreEqual(4, regions.Count());

                    Assert.AreEqual("RtlFreeHeap", regions[0].Key.Symbol.Name);
                    Assert.AreEqual(4, regions[0].Count());

                    Assert.AreEqual("RtlpTpWaitFinalizationCallback", regions[1].Key.Symbol.Name);
                    Assert.AreEqual(3, regions[1].Count());

                    Assert.AreEqual("RtlpFreeDebugInfo", regions[2].Key.Symbol.Name);
                    Assert.AreEqual(1, regions[2].Count());

                    Assert.AreEqual("RtlInterlockedPushEntrySList", regions[3].Key.Symbol.Name);
                    Assert.AreEqual(2, regions[3].Count());
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_OverlappingSymbols_MultipleSymbolsInFunction_RtlpInterlockedPopEntrySList()
        {
            //RtlpInterlockedPopEntrySList contains multiple symbols within it

            TestMatchAllFunctions(
                WellKnownTestModule.Ntdll,
                PEMetadataSearchOptions.Symbols,
                (s, _) => new MockSymbolModule(s, "RtlpInterlockedPopEntrySList", "ExpInterlockedPopEntrySListResume", "ExpInterlockedPopEntrySListFault", "ExpInterlockedPopEntrySListEnd"),
                m =>
                {
                    var regions = GetFunctionsWithSymbols(m).GroupBy(v => v.Function.PrimaryMetadata).ToArray();

                    Assert.AreEqual(1, regions.Length);
                    Assert.AreEqual("RtlpInterlockedPopEntrySList", regions[0].Key.Symbol.Name);

                    var items = regions[0].ToArray();

                    Assert.AreEqual(4, regions[0].Count());
                    Assert.AreEqual("RtlpInterlockedPopEntrySList", items[0].Metadata.Symbol.Name);
                    Assert.AreEqual("ExpInterlockedPopEntrySListResume", items[1].Metadata.Symbol.Name);
                    Assert.AreEqual("ExpInterlockedPopEntrySListFault", items[2].Metadata.Symbol.Name);
                    Assert.AreEqual("ExpInterlockedPopEntrySListEnd", items[3].Metadata.Symbol.Name);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_OverlappingSymbols_ConflictingChunks_RtlPrefixString()
        {
            //None of the normal checks apply, and yet we still have conflicting instructions

            TestSymbolsAndUnwindData(
                new[] { "RtlPrefixString" },
                m =>
                {
                    var region = GetRegion(m, "RtlPrefixString");

                    Assert.AreEqual(3, region.Function.Chunks.Count);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_OverlappingSymbols_ConflictingInstructions_TpSetTimerAndTpSetTimerEx()
        {
            //TpSetTimer jumps to TpSetTimerEx, which has an UnwindData item that needs to be deleted
            TestSymbolsAndUnwindData(
                new[] { "TpSetTimer", "TpSetTimerEx" },
                m =>
                {
                    var regions = GetFunctionsWithSymbols(m).GroupBy(v => v.Function.PrimaryMetadata).ToArray();

                    Assert.AreEqual(2, regions.Length);

                    Assert.AreEqual("TpSetTimer", regions[0].Key.Symbol.Name);
                    Assert.AreEqual(1, regions[0].Count());

                    Assert.AreEqual("TpSetTimerEx", regions[1].Key.Symbol.Name);
                    Assert.AreEqual(3, regions[1].Count());
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_OverlappingSymbols_ConflictingInstructions_TpSetTimerEx()
        {
            TestSymbolsAndUnwindData(
                new[] { "TpSetTimerEx" },
                m =>
                {
                    var regions = GetFunctionsWithSymbols(m).DistinctBy(v => v.Metadata).ToArray();

                    Assert.AreEqual(3, regions.Length);
                    Assert.AreEqual("TpSetTimerEx", regions[0].Metadata.Symbol.Name);
                    Assert.IsNull(regions[1].Metadata);
                    Assert.AreEqual(FoundBy.RuntimeFunction, regions[2].Metadata.FoundBy);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_OverlappingSymbols_SingleByteChunk_FindLabelEnd()
        {
            //We have a chunk containing a single byte. StartAddress == EndAddress and that should be OK

            TestSymbolsAndUnwindData(
                new[] { "FindLabelEnd" },
                m =>
                {
                    var region = GetRegion(m, "FindLabelEnd");
                    Assert.AreEqual(4, region.Function.Chunks.Count);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_OverlappingSymbols_EmptyChunk_RtlEnclaveCallDispatcher()
        {
            /* RtlEnclaveCallDispatcher has a symbol RtlEnclaveCallDispatchReturn inside it, and RtlEnclaveCallDispatcher
             * contains a jump at the beginning that skips OVER the start of RtlEnclaveCallDispatchReturn, into some code
             * that's listed after it. This then causes a conflict because both symbols think they own this code that's part
             * of RtlEnclaveCallDispatchReturn. Complicating matters even further, RtlEnclaveCallDispatchReturn skips OVER
             * one instruction listed after it when it jumps. Thus, our deduper concludes that RtlEnclaveCallDispatcher
             * should own all this code listed after RtlEnclaveCallDispatchReturn, since the former also owns this single
             * instruction that got skipped by RtlEnclaveCallDispatchReturn */

            TestSymbolsAndUnwindData(
                new[] { "RtlEnclaveCallDispatcher", "RtlpEnclaveCallDispatchFilter", "RtlEnclaveCallDispatchReturn" },
                m =>
                {
                    var regions = GetFunctionsWithSymbols(m).GroupBy(v => v.Function.PrimaryMetadata).ToArray();

                    Assert.AreEqual(2, regions.Length);

                    Assert.AreEqual("RtlpEnclaveCallDispatchFilter", regions[0].Key.Symbol.Name);
                    Assert.AreEqual(1, regions[0].Count());

                    Assert.AreEqual("RtlEnclaveCallDispatcher", regions[1].Key.Symbol.Name);
                    Assert.AreEqual(2, regions[1].Count());
                    Assert.AreEqual("RtlEnclaveCallDispatcher", regions[1].First().Metadata.Symbol.Name);
                    Assert.AreEqual("RtlEnclaveCallDispatchReturn", regions[1].Last().Metadata.Symbol.Name);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_OverlappingSymbols_Data_RtlNtdllName()
        {
            TestMatchAllFunctions(
                WellKnownTestModule.Ntdll,
                PEMetadataSearchOptions.Symbols | PEMetadataSearchOptions.Exports,
                (s, _) => new MockSymbolModule(s, "RtlNtdllName"),
                m =>
                {
                    var rtlNtdllName = GetData(m, "RtlNtdllName");
                    Assert.IsNotNull(rtlNtdllName);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_OverlappingSymbols_RtlEnumerateEntryHashTable()
        {
            TestMatchAllFunctions(
                WellKnownTestModule.Ntdll,
                PEMetadataSearchOptions.Symbols | PEMetadataSearchOptions.Exports,
                (s, _) => new MockSymbolModule(s), //Only need exports
                m =>
                {
                    /* This test catches the following tricky scenario:
                     *
                     * RtlWeaklyEnumerateEntryHashTable
                     *              |
                     *      RtlEnumerateEntryHashTable
                     *        |               |
                     *     18006CCD0          |
                     *                        |
                     *                        |
                     *                    18006CCD9
                     *     /            |             |            \
                     * 1800C8E7E    18006CD38     18006CD3E    18006CD44
                     *
                     * RtlEnumerateEntryHashTable absorbs both of its children (18006CCD0 and 18006CCD9).
                     * As a result, RtlEnumerateEntryHashTable should have established new relationships
                     * with the grandchildren of 18006CCD9, giving the following graph
                     *
                     * RtlWeaklyEnumerateEntryHashTable
                     *              |
                     *      RtlEnumerateEntryHashTable
                     *         |               |
                     *     1800C8E7E       18006CD38
                     *
                     */

                    var weakRegions = GetFunctionRegions(m, "RtlWeaklyEnumerateEntryHashTable");
                    Assert.AreEqual(1, weakRegions.Length);

                    var fullRegions = GetFunctionRegions(m, "RtlEnumerateEntryHashTable");
                    Assert.AreEqual(3, fullRegions.Length);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_OverlappingSymbols_OneFunction_JumpsToTwoOthers_WhichMergeSomewhereElse()
        {
            TestMatchAllFunctions(
                WellKnownTestModule.Ntdll,
                PEMetadataSearchOptions.Symbols,
                (s, _) => new MockSymbolModule(s, "RtlpCSparseBitmapUnlock", "RtlReleaseSRWLockShared", "RtlReleaseSRWLockExclusive", "RtlpWakeSRWLock"),
                m =>
                {
                    var regions = GetFunctionsWithSymbols(m).DistinctBy(v => v.Metadata).ToArray();

                    Assert.AreEqual(5, regions.Length);

                    Assert.AreEqual("RtlReleaseSRWLockShared", regions[0].Metadata.Symbol.Name);
                    Assert.IsNull(regions[1].Metadata);
                    Assert.AreEqual("RtlpWakeSRWLock", regions[2].Metadata.Symbol.Name);
                    Assert.AreEqual("RtlReleaseSRWLockExclusive", regions[3].Metadata.Symbol.Name);
                    Assert.AreEqual("RtlpCSparseBitmapUnlock", regions[4].Metadata.Symbol.Name);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_OverlappingSymbols_OneFunctionJumpsToAnother_WhichHasOverlappingUnwindData()
        {
            TestSymbolsAndUnwindData(
                new[] { "RtlAcquireSRWLockExclusive", "LdrForkMrdata", "RtlpWakeSRWLock" },
                m =>
                {
                    var regions = GetFunctionsWithSymbols(m).DistinctBy(v => v.Metadata).ToArray();

                    Assert.AreEqual(7, regions.Length);

                    Assert.AreEqual("RtlpWakeSRWLock", regions[0].Metadata.Symbol.Name);
                    Assert.IsNull(regions[1].Metadata);
                    Assert.AreEqual("RtlAcquireSRWLockExclusive", regions[2].Metadata.Symbol.Name);
                    Assert.AreEqual(FoundBy.RuntimeFunction, regions[3].Metadata.FoundBy);
                    Assert.AreEqual(FoundBy.RuntimeFunction, regions[4].Metadata.FoundBy);
                    Assert.AreEqual(FoundBy.RuntimeFunction, regions[5].Metadata.FoundBy);
                    Assert.AreEqual("LdrForkMrdata", regions[6].Metadata.Symbol.Name);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_Issue_LdrUnloadAlternateResourceModuleEx()
        {
            //Nobody claims {ntdll!LdrUnloadAlternateResourceModuleEx$fin$0} and it becomes standalone!
            TestSymbolsAndUnwindData(
                new[] { "LdrUnloadAlternateResourceModule", "LdrUnloadAlternateResourceModuleEx", "LdrUnloadAlternateResourceModuleEx$fin$0", "__C_specific_handler" },
                m =>
                {
                    var regions = GetFunctionsWithSymbols(m).DistinctBy(v => v.Function.PrimaryMetadata).ToArray();

                    //LdrUnloadAlternateResourceModuleEx$fin$0 should be part of LdrUnloadAlternateResourceModuleEx
                    Assert.AreEqual(0x1800311C0, regions[0].Metadata.Address); //RtlpWakeSRWLock

                    Assert.AreEqual("LdrUnloadAlternateResourceModule", regions[1].Metadata.Symbol.Name);
                    Assert.AreEqual(1, regions[1].Function.AllRegions.Count());

                    Assert.AreEqual("LdrUnloadAlternateResourceModuleEx", regions[2].Metadata.Symbol.Name);
                    var handler = GetRegion(m, "LdrUnloadAlternateResourceModuleEx$fin$0");
                    Assert.AreEqual(regions[2].Function, handler.Function);

                    Assert.AreEqual("_C_specific_handler", regions[3].Metadata.Symbol.Name);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_ExceptionHandler_TryExcept()
        {
            TestSymbolsAndUnwindData(
                new[] { "LdrpProtectedCopyMemory", "LdrpProtectedCopyMemory$filt$0", "__C_specific_handler" },
                m =>
                {
                    var regions = GetFunctionsWithSymbols(m).DistinctBy(v => v.Metadata).ToArray();

                    Assert.AreEqual(3, regions.Length);

                    Assert.AreEqual("LdrpProtectedCopyMemory", regions[0].Metadata.Symbol.Name);
                    Assert.AreEqual("_C_specific_handler", regions[1].Metadata.Symbol.Name);
                    Assert.AreEqual("LdrpProtectedCopyMemory$filt$0", regions[2].Metadata.Symbol.Name);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_ExceptionHandler_TppWorkerThread()
        {
            TestSymbolsAndUnwindData(
                new[] { "TppWorkerThread", "__GSHandlerCheck_SEH" },
                m =>
                {
                    var regions = GetFunctionsWithSymbols(m).DistinctBy(v => v.Function.PrimaryMetadata).ToArray();

                    Assert.AreEqual(2, regions.Length);
                    Assert.AreEqual(13, regions[0].Function.AllRegions.Count());
                    Assert.AreEqual(1, regions[1].Function.AllRegions.Count());
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_RVA_LdrAddDllDirectory()
        {
            //18007851E does a lea of a function pointer that is then passed to local_unwind.
            //This method is referenced in __guard_eh_cont_table, but we need to be able to detect
            //function pointers even without Guard EH information

            TestSymbolsAndUnwindData(
                new[] { "LdrAddDllDirectory", "__GSHandlerCheck_SEH" },
                m =>
                {
                    var regions = GetFunctionsWithSymbols(m);

                    //There's also a scope table entry containing the unwind handler, which we don't see without specifying __GSHandlerCheck_SEH
                    //due to the fact that we need to identify which unwind handler is used in order to know how to interpret the ExceptionData

                    Assert.AreEqual(7, regions.Length);
                    Assert.AreEqual(132, regions[0].Function.AllInstructions.Count());

                    Assert.AreEqual("LdrAddDllDirectory", regions[0].Metadata.Symbol.Name);
                    Assert.AreEqual(0x1800783C0, regions[0].PhysicalStartAddress);
                    Assert.AreEqual(107, regions[0].Instructions.Length);

                    Assert.IsNull(regions[1].Metadata);
                    Assert.AreEqual(0x1800785AF, regions[1].PhysicalStartAddress);
                    Assert.AreEqual(7, regions[1].Instructions.Length);

                    Assert.AreEqual("_GSHandlerCheck_SEH", regions[2].Metadata.Symbol.Name);
                    Assert.AreEqual(0x18009EE2C, regions[2].PhysicalStartAddress);
                    Assert.AreEqual(40, regions[2].Instructions.Length);

                    //This item is only discovered because we can read the ExceptionData when we know the handler is __GSHandlerCheck_SEH
                    Assert.AreEqual(FoundBy.UnwindInfo, regions[3].Metadata.FoundBy);
                    Assert.AreEqual(0x1800A920F, regions[3].PhysicalStartAddress);
                    Assert.AreEqual(9, regions[3].Instructions.Length);

                    Assert.AreEqual(FoundBy.RuntimeFunction, regions[4].Metadata.FoundBy);
                    Assert.AreEqual(0x1800CB93E, regions[4].PhysicalStartAddress);
                    Assert.AreEqual(2, regions[4].Instructions.Length);

                    Assert.AreEqual(FoundBy.RVA, regions[5].Metadata.FoundBy);
                    Assert.AreEqual(0x1800CB948, regions[5].PhysicalStartAddress);
                    Assert.AreEqual(2, regions[5].Instructions.Length);

                    Assert.IsNull(regions[6].Metadata);
                    Assert.AreEqual(0x1800CB952, regions[6].PhysicalStartAddress);
                    Assert.AreEqual(7, regions[6].Instructions.Length);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_OrphanedRegion_RtlpUnwindEpilogue()
        {
            //RtlpUnwindEpilogue has a block of code referenced from the Guard EH table that sits between two chunks,
            //and even flows into them (i.e. it doesn't end in a terminating statement; it ends in a mov). On that basis,
            //this code should be considered "part" of RtlpUnwindEpilogue
            TestMatchAllFunctions(
                WellKnownTestModule.Ntdll,
                PEMetadataSearchOptions.Symbols | PEMetadataSearchOptions.Config,
                (s, _) => new MockSymbolModule(s, "RtlpUnwindEpilogue"),
                m =>
                {
                    var function = GetRegion(m, "RtlpUnwindEpilogue").Function;

                    var rogueRegion = (INativeFunctionChunkRegion) m.FindByPhysicalAddress(0x180110327);

                    Assert.AreEqual(function, rogueRegion.Function);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_OverlappingSymbols_XFG_Targets()
        {
            TestMatchAllFunctions(
                WellKnownTestModule.Ntdll,
                PEMetadataSearchOptions.Symbols,
                (s, _) => new MockSymbolModule(s, "LdrpReadMemory", "__guard_dispatch_icall_fptr", "__guard_xfg_check_icall_fptr", "__guard_xfg_dispatch_icall_fptr", "__guard_xfg_table_dispatch_icall_fptr", "__castguard_check_failure_os_handled_fptr"),
                m =>
                {
                    var regions = GetFunctionRegions(m, "LdrpReadMemory");
                    Assert.AreEqual(1, regions.Length);

                    var call = (XRefAwareNativeInstruction) regions[0].Instructions.Single(i => i.Instruction.Mnemonic == Mnemonic.Call);
                    var target = (DataMetadataRange) ((NativeCallXRefInfo) call.RefsFromThis.Single()).Target;
                    Assert.AreEqual("_guard_xfg_dispatch_icall_fptr", target.Metadata.Symbol.Name);
                }
            );
        }

        [TestMethod]
        public void PEModuleMetadata_CompareIDA_Ntdll()
        {
            CompareIDA(WellKnownTestModule.Ntdll, v =>
            {
                Assert.AreEqual(2, v.Count);

                //strcmp should end in 8 garbage bytes that IDA erroneously detected as being code. These aren't XFG bytes,
                //as they're followed by 0xcc bytes and then a REAL XFG for the next function
                Assert.AreEqual("strcmp", v[0].ida.Name);
                Assert.AreEqual(3, v[0].addresses.Length);

                //There's 7 garbage bytes after the end of RtlpPlaceActivationContextOnLiveList prior to
                //RtlpPlaceActivationContextOnLiveList$fin$0
                Assert.AreEqual("RtlpPlaceActivationContextOnLiveList", v[1].ida.Name);
                Assert.AreEqual(3, v[1].addresses.Length);
            });
        }

        private void CompareIDA(SymbolStoreKey key, Action<List<(IDAFunctionMetadata ida, INativeFunctionChunkRegion chaos, long[] addresses)>> validateMissing)
        {
            TestMatchAllFunctions(key, PEMetadataSearchOptions.All, null, m =>
            {
                var lstFile = WellKnownTestModule.GetIDALst(m.FileName);

                var idaRoutines = IDAComparer.GetIDARoutines(lstFile, m);

                var missing = IDAComparer.GetMissingIDACode(idaRoutines, m);
                validateMissing(missing);

                IDAComparer.CompareIDAFunctionRanges(new IdaContext(idaRoutines), new ChaosContext(m.Ranges), m);
            });
        }

        private NativeFunctionChunkRegion<InstructionDiscoverySource>[] GetFunctionsWithSymbols(PEMetadataModule module) =>
            module.Ranges.OfType<NativeFunctionChunkRegion<InstructionDiscoverySource>>().Where(v => v.Function.PrimaryMetadata != null && v.Function.PrimaryMetadata.FoundBy != FoundBy.Call).ToArray();

        private NativeFunctionChunkRegion<InstructionDiscoverySource>[] GetFunctionRegions(PEMetadataModule module, string name) =>
            GetFunctionsWithSymbols(module).Where(v => v.Function.PrimaryMetadata.Symbol?.Name == name || v.Function.PrimaryMetadata.Export?.Name == name).ToArray();

        private NativeFunctionChunkRegion<InstructionDiscoverySource> GetRegion(PEMetadataModule module, string name) =>
            GetFunctionsWithSymbols(module).Single(v => v.Metadata?.Symbol?.Name == name || v.Metadata?.Export?.Name == name);

        private DataMetadataRange GetData(PEMetadataModule module, string name) =>
            module.Ranges.OfType<DataMetadataRange>().Single(v => v.Metadata?.Symbol?.Name == name || v.Metadata?.Export?.Name == name);

        private unsafe void TestMatchAllFunctions(
            SymbolStoreKey moduleKey,
            PEMetadataSearchOptions options,
            Func<IUnmanagedSymbolModule, INativeDisassembler, IUnmanagedSymbolModule> mockSymbolModule,
            Action<PEMetadataPhysicalModule> validate,
            bool liveNtdll = false)
        {
            new NativeLibraryProvider().GetModuleHandle(WellKnownNativeLibrary.DbgHelp);

            var hProcess = Process.GetCurrentProcess().Handle;

            DbgHelpSession dbgHelp;
            string path;
            Stream stream;

            if (liveNtdll)
            {
                dbgHelp = new DbgHelpSession(hProcess);
                path = dbgHelp.LoadModule("ntdll.dll").FilePath;
                stream = new ProcessMemoryStream(hProcess);
            }
            else
            {
                dbgHelp = DbgHelpProvider.Acquire();
                path = WellKnownTestModule.GetStoreFile(moduleKey);
                dbgHelp.AddPhysicalModule(path);
                var fileStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var peFile = new PEFile(fileStream, false);
                stream = new AbsoluteToRelativeStream(fileStream, peFile.OptionalHeader.ImageBase);
            }

            try
            {
                var dbgHelpModule = dbgHelp.Modules.First();

                var metadataProvider = GetService<PEMetadataProvider>();

                var is32Bit = IntPtr.Size == 4;
                var symbolResolver = new DbgHelpDisasmSymbolResolver(dbgHelp);

                IUnmanagedSymbolModule symbolModule;

                if (mockSymbolModule == null)
                    symbolModule = dbgHelpModule;
                else
                {
                    var nativeDisassemblerProvider = GetService<INativeDisassemblerProvider>();

                    var disassembler = nativeDisassemblerProvider.CreateDisassembler(stream, is32Bit, symbolResolver);

                    symbolModule = mockSymbolModule(dbgHelpModule, disassembler);
                }

                PEMetadataPhysicalModule metadataModule;

                if (liveNtdll)
                {
                    metadataModule = metadataProvider.GetVirtualMetadata(
                        hProcess,
                        (IntPtr) dbgHelpModule.Address,
                        symbolModule,
                        (RemoteMemoryStream) stream,
                        symbolResolver,
                        options
                    );
                }
                else
                {
                    metadataModule = metadataProvider.GetPhysicalMetadata(
                        path,
                        symbolModule,
                        symbolResolver,
                        options
                    );
                }

                validate(metadataModule);
            }
            finally
            {
                stream?.Dispose();
            }
        }

        private void TestSymbolsAndExports(string[] allowedSymbols, Action<PEMetadataPhysicalModule> validate)
        {
            var mockPEProvider = (MockPEFileProvider) GetService<IPEFileProvider>();

            TestMatchAllFunctions(
                WellKnownTestModule.Ntdll,
                PEMetadataSearchOptions.Symbols | PEMetadataSearchOptions.Exports,
                (s, d) =>
                {
                    var mockSymbolModule = new MockSymbolModule(s, allowedSymbols);

                    mockPEProvider.ConfigureMock = v =>
                    {
                        if (v.ExceptionDirectory == null)
                            return;

                        var symbols = mockSymbolModule.EnumerateSymbols().ToArray();

                        if (v.ExportDirectory != null)
                            ReflectionExtensions.SetPropertyValue(v.ExportDirectory, nameof(ImageExportDirectoryInfo.Exports), v.ExportDirectory.Exports.Where(e => symbols.Any(sym => sym.Name == e.Name)).ToArray());
                    };

                    return mockSymbolModule;
                },
                validate
            );
        }

        private void TestSymbolsAndUnwindData(
            string[] allowedSymbols,
            Action<PEMetadataPhysicalModule> validate,
            bool liveNtdll = false)
        {
            var mockPEProvider = (MockPEFileProvider) GetService<IPEFileProvider>();

            TestMatchAllFunctions(
                WellKnownTestModule.Ntdll,
                PEMetadataSearchOptions.Symbols | PEMetadataSearchOptions.UnwindData,
                (s, d) =>
                {
                    var mockSymbolModule = new MockSymbolModule(s, allowedSymbols);

                    mockPEProvider.ConfigureMock = v =>
                    {
                        if (v.ExceptionDirectory == null)
                            return;

                        var symbols = mockSymbolModule.EnumerateSymbols().ToArray();

                        //Our Either doesn't throw on accessing the wrong value
                        var functions = symbols.Select(sym => d.DisassembleCodeRegions(sym.Address)).Where(r => r.IsSuccess).ToArray();

                        var allowed = v.ExceptionDirectory.Where(e => functions.Any(f =>
                        {
                            if (f.Contains(e.BeginAddress + mockSymbolModule.Address))
                                return true;

                            //todo: why is exceptiondata a bunch of cc's sometimes?
                            if (e.UnwindData.TryGetExceptionData(out var exceptionData) && exceptionData is ImageScopeTable table)
                            {
                                foreach (var record in table)
                                {
                                    if (f.Contains(record.BeginAddress + mockSymbolModule.Address))
                                        return true;

                                    if (f.Contains(record.EndAddress + mockSymbolModule.Address))
                                        return true;

                                    if (record.HandlerAddress > 1 && f.Contains(record.HandlerAddress + mockSymbolModule.Address))
                                        return true;

                                    if (record.JumpTarget != 0 && f.Contains(record.JumpTarget + mockSymbolModule.Address))
                                        return true;
                                }
                            }

                            return false;
                        })).ToArray();

                        //For each symbol we want to consider, disassemble the function to get all its chunks, and then get all unwind items that exist within those chunks
                        ReflectionExtensions.SetPropertyValue(v, nameof(PEFile.ExceptionDirectory), allowed);
                        ReflectionExtensions.SetPropertyValue(v, nameof(PEFile.ExportDirectory), null);
                    };

                    return mockSymbolModule;
                },
                validate,
                liveNtdll
            );
        }

        private void VerifyJumpTable(PEMetadataPhysicalModule module, INativeFunctionChunkRegion mainRegion, JumpTableMetadataRange jumpTable)
        {
            //Verify that each target of the jump table was included in the function

            var jumpTableTargets = jumpTable.Targets.Select(v => (INativeFunctionChunkRegion) module.FindByPhysicalAddress(v)).ToArray();

            foreach (var target in jumpTableTargets)
                Assert.AreEqual(mainRegion.Function.PrimaryMetadata, target.Function.PrimaryMetadata);
        }
    }
}
