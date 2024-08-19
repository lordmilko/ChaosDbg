using System;
using System.Collections.Generic;
using System.Linq;
using ChaosDbg.Cordb;
using ChaosDbg.IL;
using ChaosLib.Metadata;
using ChaosLib.PortableExecutable;
using ClrDebug;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestApp;

namespace ChaosDbg.Tests
{
    [MTATestClass]
    public class CordbEngineDisasmTests : BaseTest
    {
        [TestMethod]
        public void CordbEngine_Disasm_IL()
        {
            TestSignalledDebugCreate(
                TestType.CordbEngine_Thread_StackTrace_ManagedFrames,
                ctx =>
                {
                    var thread = ctx.CordbEngine.Process.Threads.Single();

                    thread.Verify().IL(
                        "System.Threading.Thread.Sleep",
                        "IL_0000   ldarg.0",
                        "IL_0001   call System.Threading.Thread.SleepInternal",
                        "IL_0006   call System.AppDomainPauseManager.get_IsPaused",
                        "IL_000B   brfalse.s IL_0018",
                        "IL_000D   call System.AppDomainPauseManager.get_ResumeEvent",
                        "IL_0012   callvirt System.Threading.WaitHandle.WaitOneWithoutFAS",
                        "IL_0017   pop",
                        "IL_0018   ret"
                    );
                }
            );
        }

        [TestMethod]
        public void CordbEngine_Disasm_Native()
        {
            TestSignalledDebugCreate(
                TestType.CordbEngine_Thread_StackTrace_ManagedFrames,
                ctx =>
                {
                    var thread = ctx.CordbEngine.Process.Threads.Single();

                    var x86Expected = new[]
                    {
                        "55              push    ebp",
                        "8bec            mov     ebp,esp",
                        "83ec10          sub     esp,10h",
                        "33c0            xor     eax,eax",
                        "8945f0          mov     dword ptr [ebp-10h],eax",
                        "894dfc          mov     dword ptr [ebp-4],ecx",
                        "8b05a813d37a    mov     eax,dword ptr [mscorlib.ni+0x13a8 (7ad313a8)] ds:7ad313a8={mscorlib.ni+0x12ac}",
                        "833800          cmp     dword ptr [eax],0",
                        "7406            je      mscorlib!System.Threading.Thread.Sleep(Int32)+0x1f",
                        "ff156095027b    call    dword ptr [mscorlib.ni+0x3a9560 (7b0d9560)] ds:7b0d9560={CORINFO_HELP_DBG_IS_JUST_MY_CODE (clr!JIT_DbgIsJustMyCode)}",
                        "8b4dfc          mov     ecx,dword ptr [ebp-4]",
                        "ff15388ac87a    call    dword ptr [mscorlib.ni+0x8a38 (7ad38a38)] ds:7ad38a38={clr!ThreadNative::Sleep}",
                        "ff15dc93eb7a    call    dword ptr [mscorlib.ni+0x2393dc (7af693dc)] ds:7af693dc={mscorlib!System.AppDomainPauseManager.get_IsPaused()}",
                        "8945f8          mov     dword ptr [ebp-8],eax",
                        "837df800        cmp     dword ptr [ebp-8],0",
                        "7418            je      mscorlib!System.Threading.Thread.Sleep(Int32)+0x4f",
                        "ff15f093eb7a    call    dword ptr [mscorlib.ni+0x2393f0 (7af693f0)] ds:7af693f0={mscorlib!System.AppDomainPauseManager.get_ResumeEvent()}",
                        "8945f0          mov     dword ptr [ebp-10h],eax",
                        "8b4df0          mov     ecx,dword ptr [ebp-10h]",
                        "3909            cmp     dword ptr [ecx],ecx",
                        "ff155890cf7a    call    dword ptr [mscorlib.ni+0x79058 (7ada9058)] ds:7ada9058={mscorlib!System.Threading.WaitHandle.WaitOneWithoutFAS()}",
                        "8945f4          mov     dword ptr [ebp-0Ch],eax",
                        "90              nop",
                        "90              nop",
                        "8be5            mov     esp,ebp",
                        "5d              pop     ebp",
                        "c3              ret"
                    };

                    var x64Expected = new[]
                    {
                        "55              push    rbp",
                        "4883ec30        sub     rsp,30h",
                        "488d6c2430      lea     rbp,[rsp+30h]",
                        "33c0            xor     eax,eax",
                        "488945f0        mov     qword ptr [rbp-10h],rax",
                        "894d10          mov     dword ptr [rbp+10h],ecx",
                        "833d86e5deff00  cmp     dword ptr [<memory>],0",
                        "7405            je      mscorlib!System.Threading.Thread.Sleep(Int32)+0x21",
                        "e85fd69c5f      call    CORINFO_HELP_DBG_IS_JUST_MY_CODE (clr!JIT_DbgIsJustMyCode)",
                        "8b4d10          mov     ecx,dword ptr [rbp+10h]",
                        "e817b2605f      call    clr!ThreadNative::Sleep",
                        "e842faffff      call    mscorlib!System.AppDomainPauseManager.get_IsPaused()",
                        "0fb6c0          movzx   eax,al",
                        "8945fc          mov     dword ptr [rbp-4],eax",
                        "837dfc00        cmp     dword ptr [rbp-4],0",
                        "741b            je      mscorlib!System.Threading.Thread.Sleep(Int32)+0x55",
                        "e839faffff      call    mscorlib!System.AppDomainPauseManager.get_ResumeEvent()",
                        "488945f0        mov     qword ptr [rbp-10h],rax",
                        "488b4df0        mov     rcx,qword ptr [rbp-10h]",
                        "3909            cmp     dword ptr [rcx],ecx",
                        "e89226feff      call    mscorlib!System.Threading.WaitHandle.WaitOneWithoutFAS()",
                        "0fb6c0          movzx   eax,al",
                        "8945f8          mov     dword ptr [rbp-8],eax",
                        "90              nop",
                        "90              nop",
                        "488d6500        lea     rsp,[rbp]",
                        "5d              pop     rbp",
                        "c3              ret"
                    };

                    thread.Verify().Disasm(
                        ctx.InProcDbgEng.Value,
                        "System.Threading.Thread.Sleep",
                        x86Expected: x86Expected,
                        x64Expected: x64Expected
                    );
                }
            );
        }

        [TestMethod]
        public void CordbEngine_Disasm_ILToNative_StressTest()
        {
            TestSignalledDebugCreate(
                TestType.CordbEngine_Thread_StackTrace_ManagedFrames,
                ctx =>
                {
                    var corDebugModules = ctx.CordbEngine.Process.CorDebugProcess.AppDomains
                        .SelectMany(a => a.Assemblies)
                        .SelectMany(a => a.Modules)
                        .ToArray();

                    foreach (var corDebugModule in corDebugModules)
                    {
                        var mdi = corDebugModule.GetMetaDataInterface<MetaDataImport>();

                        var metadataProvider = new MetaDataProvider(mdi);

                        var typeDefs = mdi.EnumTypeDefs();

                        foreach (var typeDef in typeDefs)
                        {
                            var typeDefProps = mdi.GetTypeDefProps(typeDef);

                            var isClass = typeDefProps.pdwTypeDefFlags.IsTdClass();
                            var isInterface = typeDefProps.pdwTypeDefFlags.IsTdInterface();

                            if (isInterface)
                                continue;

                            var methods = mdi.EnumMethods(typeDef);

                            foreach (var method in methods)
                            {
                                var methodProps = mdi.GetMethodProps(method);

                                var function = corDebugModule.GetFunctionFromToken(method);

                                //mscordbi!CordbFunction::GetILCodeAndSigToken() will throw if its not il.
                                //Preempt this by performing the same checks that are done in CordbFunction::InitNativeImpl()
                                //to determine whether the function should be classified as native or not
                                if (methodProps.pdwImplFlags.IsMiNative())
                                    continue;

                                if (methodProps.pulCodeRVA == 0)
                                {
                                    //If the module isn't dynamic and it's not an edit and continue function, it's classified as native. We won't have done any edit-and-continuing
                                    //in this test, so we can skip that check
                                    if (!corDebugModule.IsDynamic)
                                        continue;
                                }

                                if (function.TryGetILCode(out _) == HRESULT.CORDBG_E_FUNCTION_NOT_IL)
                                    continue;
                                
                                var module = (CordbManagedModule) ctx.CordbEngine.Process.Modules.GetModuleForAddress(corDebugModule.BaseAddress);

                                var cdbFunction = new CordbILFunction(function, module);

                                if (cdbFunction.JITStatus == JITTypes.TYPE_UNKNOWN)
                                    continue;

                                var ilToNative = cdbFunction.ILToDisassembly;
                            }
                        }
                    }
                }
            );
        }

        [TestMethod]
        public void CordbEngine_Disasm_ILToNative_ContiguousIL()
        {
            //Each IL region starts directly after the previous:
            //-2 -> 0 (Prolog)
            // 0 -> 4 (Code)
            //-3      (Epilog)

            var ilBytes = new byte[]
            {
                0x02, 0x02, 0x8e, 0x69, 0x28, 0x15, 0x00, 0x00, 0x0a, 0x2a
            };

            var nativeChunk = new CodeChunkInfo
            {
                startAddr = 0x7FFC2A9310B0,
                length = 19
            };

            var mapping = new[]
            {
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = -2, nativeStartOffset = 0,  nativeEndOffset = 4  },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = 0,  nativeStartOffset = 4,  nativeEndOffset = 13 },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = -3, nativeStartOffset = 13, nativeEndOffset = 19 }
            };

            var readMemory = new Dictionary<CORDB_ADDRESS, byte[]>
            {
                //CodeChunkInfo bytes
                { 0x7FFC2A9310B0, new byte[]
                {
                    0x48, 0x83, 0xec, 0x28, 0x8b, 0x51, 0x08, 0xff, 0x15, 0xfb, 0x0f, 0x00, 0x00, 0x90, 0x48, 0x83, 0xc4, 0x28, 0xc3
                } }
            };

            TestILToNative(
                ilBytes,
                nativeChunk,
                mapping,
                readMemory,
                r =>
                {
                    Assert.AreEqual(3, r.Length);

                    Assert.AreEqual(ILToNativeInstructionKind.Prolog, r[0].Kind);
                    Assert.AreEqual(0, r[0].IL.Length);
                    r[0].Mapping.Verify(ilOffset: -2, nativeStartOffset: 0, nativeEndOffset: 4);
                    Assert.AreEqual(1, r[0].NativeInstructions.Length);

                    Assert.AreEqual(ILToNativeInstructionKind.Code, r[1].Kind);
                    Assert.AreEqual(6, r[1].IL.Length);
                    r[1].Mapping.Verify(ilOffset: 0, nativeStartOffset: 4, nativeEndOffset: 13);
                    Assert.AreEqual(2, r[1].NativeInstructions.Length);


                    Assert.AreEqual(ILToNativeInstructionKind.Epilog, r[2].Kind);
                    Assert.AreEqual(0, r[2].IL.Length);
                    r[2].Mapping.Verify(ilOffset: -3, nativeStartOffset: 13, nativeEndOffset: 19);
                    Assert.AreEqual(3, r[2].NativeInstructions.Length);
                }
            );
        }

        [TestMethod]
        public void CordbEngine_Disasm_ILToNative_PrologExtendsIntoIL()
        {
            //The prolog section (-2) extends into the actual IL bytes. There isn't an COR_DEBUG_IL_TO_NATIVE_MAP
            //entry whose ilOffset is 0

            //System.Management.Automation.PSTraceSource.GetNewTraceSource()

            var ilBytes = new byte[]
            {
                0x02, 0x28, 0x0e, 0x01, 0x00, 0x0a, 0x2c, 0x0b, 0x72, 0x29, 0x7d, 0x05, 0x70, 0x73, 0xf9,
                0x02, 0x00, 0x0a, 0x7a, 0x02, 0x02, 0x03, 0x04, 0x73, 0xdd, 0x41, 0x00, 0x06, 0x2a
            };

            var nativeChunk = new CodeChunkInfo
            {
                startAddr = 0x7FFC152AA770,
                length = 114
            };

            var mapping = new[]
            {
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = -2, nativeStartOffset = 0,   nativeEndOffset = 30 },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = 8,  nativeStartOffset = 76,  nativeEndOffset = 104 },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = 18, nativeStartOffset = 104, nativeEndOffset = 114 },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = 19, nativeStartOffset = 30,  nativeEndOffset = 64 },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = 28, nativeStartOffset = 64,  nativeEndOffset = 67 },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = -3, nativeStartOffset = 67,  nativeEndOffset = 76 }
            };

            var readMemory = new Dictionary<CORDB_ADDRESS, byte[]>
            {
                //CodeChunkInfo bytes
                { 0x00007ffc152aa770, new byte[]
                {
                    0x57, 0x56, 0x55, 0x53, 0x48, 0x83, 0xec, 0x28, 0x48, 0x8b, 0xf1, 0x48, 0x8b, 0xfa, 0x41,
                    0x8b, 0xd8, 0x48, 0x8b, 0xce, 0xff, 0x15, 0xde, 0x1a, 0xbc, 0x00, 0x84, 0xc0, 0x75, 0x2e,
                    0xff, 0x15, 0x44, 0xcc, 0xba, 0x00, 0x48, 0x8b, 0xe8, 0x0f, 0xb6, 0xcb, 0x89, 0x4c, 0x24,
                    0x20, 0x48, 0x8b, 0xcd, 0x48, 0x8b, 0xd6, 0x4c, 0x8b, 0xc6, 0x4c, 0x8b, 0xcf, 0xff, 0x15,
                    0xe0, 0x72, 0xbd, 0x00, 0x48, 0x8b, 0xc5, 0x48, 0x83, 0xc4, 0x28, 0x5b, 0x5d, 0x5e, 0x5f,
                    0xc3, 0xff, 0x15, 0xd6, 0xfa, 0xba, 0x00, 0x48, 0x8b, 0xf0, 0x48, 0x8b, 0x15, 0x84, 0x7c,
                    0xc0, 0x00, 0x48, 0x8b, 0x12, 0x48, 0x8b, 0xce, 0xff, 0x15, 0x18, 0x1c, 0xbc, 0x00, 0x48,
                    0x8b, 0xce, 0xff, 0x15, 0x17, 0x59, 0xba, 0x00, 0xcc
                } }
            };

            TestILToNative(
                ilBytes,
                nativeChunk,
                mapping,
                readMemory,
                r =>
                {
                    Assert.AreEqual(6, r.Length);

                    Assert.AreEqual(ILToNativeInstructionKind.Prolog, r[0].Kind);
                    Assert.AreEqual(3, r[0].IL.Length);
                    r[0].Mapping.Verify(ilOffset: -2, nativeStartOffset: 0, nativeEndOffset: 30);
                    Assert.AreEqual(12, r[0].NativeInstructions.Length);

                    Assert.AreEqual(ILToNativeInstructionKind.Code, r[1].Kind);
                    Assert.AreEqual(2, r[1].IL.Length);
                    r[1].Mapping.Verify(ilOffset: 8, nativeStartOffset: 76, nativeEndOffset: 104);
                    Assert.AreEqual(6, r[1].NativeInstructions.Length);

                    Assert.AreEqual(ILToNativeInstructionKind.Code, r[2].Kind);
                    Assert.AreEqual(1, r[2].IL.Length);
                    r[2].Mapping.Verify(ilOffset: 18, nativeStartOffset: 104, nativeEndOffset: 114);
                    Assert.AreEqual(3, r[2].NativeInstructions.Length);

                    Assert.AreEqual(ILToNativeInstructionKind.Code, r[3].Kind);
                    Assert.AreEqual(5, r[3].IL.Length);
                    r[3].Mapping.Verify(ilOffset: 19, nativeStartOffset: 30, nativeEndOffset: 64);
                    Assert.AreEqual(9, r[3].NativeInstructions.Length);

                    Assert.AreEqual(ILToNativeInstructionKind.Code, r[4].Kind);
                    Assert.AreEqual(1, r[4].IL.Length);
                    r[4].Mapping.Verify(ilOffset: 28, nativeStartOffset: 64, nativeEndOffset: 67);
                    Assert.AreEqual(1, r[4].NativeInstructions.Length);

                    Assert.AreEqual(ILToNativeInstructionKind.Epilog, r[5].Kind);
                    Assert.AreEqual(0, r[5].IL.Length);
                    r[5].Mapping.Verify(ilOffset: -3, nativeStartOffset: 67, nativeEndOffset: 76);
                    Assert.AreEqual(6, r[5].NativeInstructions.Length);
                }
            );
        }

        [TestMethod]
        public void CordbEngine_Disasm_ILToNative_TwoPrologEntries()
        {
            //System.Threading.WaitHandle.WaitOneNoCheck

            var ilBytes = new byte[]
            {
                0x02, 0x7b, 0x8c, 0x0b, 0x00, 0x04, 0x25, 0x2d, 0x0d, 0x26, 0x14, 0x28, 0x85, 0x1b, 0x00, 0x06, 0x73, 0xc6, 0x15, 0x00, 0x06, 0x7a,
                0x0a, 0x16, 0x0b, 0x06, 0x12, 0x01, 0x6f, 0x03, 0x4e, 0x00, 0x06, 0x28, 0xb2, 0x2c, 0x00, 0x06, 0x0d, 0x09, 0x2c, 0x22, 0x09, 0x6f,
                0xb4, 0x2c, 0x00, 0x06, 0x2c, 0x1a, 0x09, 0x17, 0x8d, 0x50, 0x01, 0x00, 0x02, 0x25, 0x16, 0x06, 0x6f, 0xfb, 0x4d, 0x00, 0x06, 0x9b,
                0x16, 0x03, 0x6f, 0xb9, 0x2c, 0x00, 0x06, 0x0c, 0x2b, 0x0d, 0x06, 0x6f, 0xfb, 0x4d, 0x00, 0x06, 0x03, 0x28, 0x09, 0x2e, 0x00, 0x06,
                0x0c, 0x08, 0x20, 0x80, 0x00, 0x00, 0x00, 0x33, 0x06, 0x73, 0x33, 0x2e, 0x00, 0x06, 0x7a, 0x08, 0x20, 0x02, 0x01, 0x00, 0x00, 0xfe,
                0x01, 0x16, 0xfe, 0x01, 0x13, 0x04, 0xde, 0x0a, 0x07, 0x2c, 0x06, 0x06, 0x6f, 0x04, 0x4e, 0x00, 0x06, 0xdc, 0x11, 0x04, 0x2a
            };

            var nativeChunk = new CodeChunkInfo
            {
                startAddr = 0x7FFC119C8AE0,
                length = 327
            };

            var mapping = new[]
            {
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = -2, nativeStartOffset = 0, nativeEndOffset = 18 },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = -2, nativeStartOffset = 280, nativeEndOffset = 300 },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = 0, nativeStartOffset = 18, nativeEndOffset = 35 },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = 21, nativeStartOffset = 270, nativeEndOffset = 280 },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = 23, nativeStartOffset = 35, nativeEndOffset = 40 },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = 25, nativeStartOffset = 40, nativeEndOffset = 50 },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = 33, nativeStartOffset = 50, nativeEndOffset = 65 },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = 33, nativeStartOffset = 112, nativeEndOffset = 120 },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = 39, nativeStartOffset = 69, nativeEndOffset = 90 },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = 50, nativeStartOffset = 153, nativeEndOffset = 221 },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = 89, nativeStartOffset = 90, nativeEndOffset = 97 },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = 97, nativeStartOffset = 120, nativeEndOffset = 138 },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = 102, nativeStartOffset = 138, nativeEndOffset = 153 },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = 103, nativeStartOffset = 97, nativeEndOffset = 112 },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = 118, nativeStartOffset = 300, nativeEndOffset = 306 },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = 121, nativeStartOffset = 306, nativeEndOffset = 318 },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = 127, nativeStartOffset = 318, nativeEndOffset = 319 },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = 128, nativeStartOffset = 221, nativeEndOffset = 223 },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = -3, nativeStartOffset = 223, nativeEndOffset = 270 },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = -3, nativeStartOffset = 319, nativeEndOffset = 327 },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = -1, nativeStartOffset = 65, nativeEndOffset = 69 }
            };

            var readMemory = new Dictionary<CORDB_ADDRESS, byte[]>
            {
                //CodeChunkInfo bytes
                { 0x7FFC119C8AE0, new byte[]
                {
                    0x55, 0x57, 0x56, 0x48, 0x83, 0xec, 0x40, 0x48, 0x8d, 0x6c, 0x24, 0x50, 0x48, 0x89, 0x65, 0xd0, 0x8b, 0xf2, 0x48, 0x8b, 0x49,
                    0x08, 0x48, 0x85, 0xc9, 0x0f, 0x84, 0xc8, 0x00, 0x00, 0x00, 0x48, 0x89, 0x4d, 0xe0, 0x33, 0xd2, 0x89, 0x55, 0xe8, 0x48, 0x8d,
                    0x55, 0xe8, 0xff, 0x15, 0x66, 0x98, 0x70, 0x00, 0xff, 0x15, 0xd0, 0x9c, 0x6f, 0x00, 0x48, 0x8b, 0x40, 0x18, 0x48, 0x85, 0xc0,
                    0x74, 0x2f, 0x48, 0x8b, 0x78, 0x10, 0x48, 0x85, 0xff, 0x75, 0x49, 0x48, 0x8b, 0x4d, 0xe0, 0x48, 0x8b, 0x49, 0x08, 0x8b, 0xd6,
                    0xff, 0x15, 0xa6, 0x77, 0x70, 0x00, 0x3d, 0x80, 0x00, 0x00, 0x00, 0x74, 0x17, 0x3d, 0x02, 0x01, 0x00, 0x00, 0x40, 0x0f, 0x95,
                    0xc7, 0x40, 0x0f, 0xb6, 0xff, 0xeb, 0x5b, 0xff, 0x15, 0x12, 0x74, 0x70, 0x00, 0xeb, 0xc9, 0xff, 0x15, 0x5a, 0xe3, 0x6f, 0x00,
                    0x48, 0x8b, 0xf0, 0x48, 0x8b, 0xce, 0xff, 0x15, 0xee, 0x77, 0x70, 0x00, 0x48, 0x8b, 0xce, 0xff, 0x15, 0xe5, 0x89, 0x6f, 0x00,
                    0x80, 0x7f, 0x08, 0x00, 0x74, 0xb1, 0xb9, 0x01, 0x00, 0x00, 0x00, 0xff, 0x15, 0x7c, 0xfe, 0x6f, 0x00, 0x48, 0x8b, 0x4d, 0xe0,
                    0x4c, 0x8b, 0x49, 0x08, 0x4c, 0x89, 0x48, 0x10, 0x48, 0x8b, 0xcf, 0x44, 0x8b, 0xce, 0x48, 0x8b, 0xd0, 0x4c, 0x8d, 0x1d, 0xd8,
                    0x6b, 0x6f, 0x00, 0x45, 0x33, 0xc0, 0xff, 0x15, 0xcf, 0x6b, 0x6f, 0x00, 0xeb, 0x8f, 0x80, 0x7d, 0xe8, 0x00, 0x74, 0x0c, 0x48,
                    0x8b, 0x4d, 0xe0, 0x33, 0xd2, 0xff, 0x15, 0xcb, 0x97, 0x70, 0x00, 0x8b, 0xc7, 0x48, 0x83, 0xc4, 0x40, 0x5e, 0x5f, 0x5d, 0xc3,
                    0xff, 0x15, 0xe3, 0xdf, 0x6f, 0x00, 0x48, 0x8b, 0xf0, 0x48, 0x8b, 0x0d, 0x71, 0x9a, 0x71, 0x00, 0x48, 0x8b, 0x09, 0xff, 0x15,
                    0x40, 0x52, 0x70, 0x00, 0x4c, 0x8b, 0xc0, 0x48, 0x8b, 0xce, 0x33, 0xd2, 0xff, 0x15, 0xea, 0x4f, 0x70, 0x00, 0x48, 0x8b, 0xce,
                    0xff, 0x15, 0x61, 0x89, 0x6f, 0x00, 0xcc, 0x55, 0x57, 0x56, 0x48, 0x83, 0xec, 0x30, 0x48, 0x8b, 0x69, 0x20, 0x48, 0x89, 0x6c,
                    0x24, 0x20, 0x48, 0x8d, 0x6d, 0x50, 0x80, 0x7d, 0xe8, 0x00, 0x74, 0x0c, 0x48, 0x8b, 0x4d, 0xe0, 0x33, 0xd2, 0xff, 0x15, 0x6a,
                    0x97, 0x70, 0x00, 0x90, 0x48, 0x83, 0xc4, 0x30, 0x5e, 0x5f, 0x5d, 0xc3,
                } }
            };

            TestILToNative(
                ilBytes,
                nativeChunk,
                mapping,
                readMemory,
                r =>
                {
                    Assert.AreEqual(21, r.Length);
                }
            );
        }

        [TestMethod]
        public void CordbEngine_Disasm_ILToNative_ThreeRegions()
        {
            //We had an issue wherein the code region wasn't claiming any instructions, and then the final region (the epilog) came straight in, saw that there
            //were unclaimed instructions and threw

            var ilBytes = new byte[]
            {
                0x02, 0x02, 0x8e, 0x69, 0x28, 0x15, 0x00, 0x00, 0x0a, 0x2a
            };

            var nativeChunk = new CodeChunkInfo
            {
                startAddr = 0x7FFC209E10B0,
                length = 19
            };

            var mapping = new[]
            {
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = -2, nativeStartOffset = 0, nativeEndOffset = 4 },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = 0,  nativeStartOffset = 4, nativeEndOffset = 13 },
                new COR_DEBUG_IL_TO_NATIVE_MAP { ilOffset = -3, nativeStartOffset = 13, nativeEndOffset = 19 }
            };

            var readMemory = new Dictionary<CORDB_ADDRESS, byte[]>
            {
                //CodeChunkInfo bytes
                { 0x7FFC209E10B0, new byte[]
                {
                    0x48, 0x83, 0xec, 0x28, 0x8b, 0x51, 0x08, 0xff, 0x15, 0xfb, 0x0f, 0x00, 0x00, 0x90, 0x48, 0x83, 0xc4, 0x28, 0xc3
                } }
            };

            TestILToNative(
                ilBytes,
                nativeChunk,
                mapping,
                readMemory,
                r =>
                {
                    Assert.AreEqual(3, r.Length);
                }
            );
        }

        private void TestILToNative(
            byte[] ilBytes,
            CodeChunkInfo nativeChunk,
            COR_DEBUG_IL_TO_NATIVE_MAP[] mapping,
            Dictionary<CORDB_ADDRESS, byte[]> readMemory,
            Action<ILToNativeInstruction[]> validate)
        {
            using var process = new CordbProcess(
                new CorDebugProcess(
                    new MockCorDebugProcess
                    {
                        ReadMemory = readMemory
                    }
                ),
                new CordbSessionInfo(
                    GetService<CordbEngineServices>(),
                    () => { },
                    default
                ),
                false,
                null
            );

            var function = new CordbILFunction(
                new CorDebugFunction(
                    new MockCorDebugFunction
                    {
                        GetILCode = new MockCorDebugCode
                        {
                            GetCode = ilBytes
                        },

                        GetNativeCode = new MockCorDebugCode
                        {
                            GetCodeChunks = new[] { nativeChunk },
                            GetILToNativeMapping = mapping
                        }
                    }
                ),

                new CordbManagedModule(
                    new CorDebugModule(
                        new MockCorDebugModule()
                    ),
                    process,
                    new PEFile()
                )
            );

            var dis = function.ILToDisassembly;

            validate(dis);
        }
    }
}
