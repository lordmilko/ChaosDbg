using System.Linq;
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
            TestDebugCreate(
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
            TestDebugCreate(
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
                        "8b05a813c87a    mov     eax,dword ptr [mscorlib.ni+0x13a8]",
                        "833800          cmp     dword ptr [eax],0",
                        "7406            je      System.Threading.Thread.Sleep(Int32)+0x1f",
                        "ff156095027b    call    dword ptr [CORINFO_HELP_DBG_IS_JUST_MY_CODE]",
                        "8b4dfc          mov     ecx,dword ptr [ebp-4]",
                        "ff15388ac87a    call    dword ptr [System.Threading.Thread.SleepInternal(Int32)]",
                        "ff15dc93eb7a    call    dword ptr [System.AppDomainPauseManager.get_IsPaused()]",
                        "8945f8          mov     dword ptr [ebp-8],eax",
                        "837df800        cmp     dword ptr [ebp-8],0",
                        "7418            je      System.Threading.Thread.Sleep(Int32)+0x4f",
                        "ff15f093eb7a    call    dword ptr [System.AppDomainPauseManager.get_ResumeEvent()]",
                        "8945f0          mov     dword ptr [ebp-10h],eax",
                        "8b4df0          mov     ecx,dword ptr [ebp-10h]",
                        "3909            cmp     dword ptr [ecx],ecx",
                        "ff155890cf7a    call    dword ptr [System.Threading.WaitHandle.WaitOneWithoutFAS()]",
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
                        "7405            je      System.Threading.Thread.Sleep(Int32)+0x21",
                        "e85fd69c5f      call    CORINFO_HELP_DBG_IS_JUST_MY_CODE",
                        "8b4d10          mov     ecx,dword ptr [rbp+10h]",
                        "e817b2605f      call    System.Threading.Thread.SleepInternal(Int32)",
                        "e842faffff      call    System.AppDomainPauseManager.get_IsPaused()",
                        "0fb6c0          movzx   eax,al",
                        "8945fc          mov     dword ptr [rbp-4],eax",
                        "837dfc00        cmp     dword ptr [rbp-4],0",
                        "741b            je      System.Threading.Thread.Sleep(Int32)+0x55",
                        "e839faffff      call    System.AppDomainPauseManager.get_ResumeEvent()",
                        "488945f0        mov     qword ptr [rbp-10h],rax",
                        "488b4df0        mov     rcx,qword ptr [rbp-10h]",
                        "3909            cmp     dword ptr [rcx],ecx",
                        "e89226feff      call    System.Threading.WaitHandle.WaitOneWithoutFAS()",
                        "0fb6c0          movzx   eax,al",
                        "8945f8          mov     dword ptr [rbp-8],eax",
                        "90              nop",
                        "90              nop",
                        "488d6500        lea     rsp,[rbp]",
                        "5d              pop     rbp",
                        "c3              ret"
                    };

                    thread.Verify().Disasm(
                        ctx.DbgEngEngine.Value,
                        "System.Threading.Thread.Sleep",
                        x86Expected: x86Expected,
                        x64Expected: x64Expected
                    );
                }
            );
        }
    }
}
