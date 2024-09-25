using System;
using System.Runtime.InteropServices;
using ChaosLib;
using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;

namespace ChaosDbg.Hook
{
    public delegate IntPtr GetProcAddressDelegate(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string name);

    class GetProcAddressConditionalHook : ConditionalHook<GetProcAddressDelegate>
    {
        public GetProcAddressConditionalHook(IntPtr fakeModule, IntPtr pOriginal, GetProcAddressDelegate hookDelegate) : base(pOriginal, hookDelegate)
        {
            try
            {
                var trampolineSize = GuessTrampolineSize();

                pTrampoline = AllocateTrampoline(trampolineSize);

                Assembler c;

                if (IntPtr.Size == 4)
                    c = WriteTrampoline32(fakeModule);
                else
                    c = WriteTrampoline64(fakeModule);

                CreateTrampoline(c, trampolineSize);
            }
            catch
            {
                if (pTrampoline != IntPtr.Zero)
                    Kernel32.VirtualFreeEx(Kernel32.GetCurrentProcess(), pTrampoline);

                throw;
            }
        }

        private Assembler WriteTrampoline32(IntPtr fakeModule)
        {
            var c = new Assembler(32);

            const int hModule = 8;
            const int lpProcName = 0xC;

            var fail = c.CreateLabel("fail");

            c.push(ebp);
            c.mov(ebp, esp);
            c.mov(eax, __[ebp + hModule]);
            c.mov(ecx, (uint) fakeModule);
            c.cmp(eax, ecx);
            c.jnz(fail);

            c.mov(__[ebp + hModule], eax); //This seems redundant, since hModule didn't change, but that's what the compiler says
            c.pop(ebp);
            c.jmp((uint) pHook);

            c.Label(ref fail);
            c.mov(__[ebp + hModule], eax);
            c.pop(ebp);
            c.jmp((uint) pOriginal);

            return c;
        }

        private unsafe Assembler WriteTrampoline64(IntPtr fakeModule)
        {
            var c = new Assembler(64);

            var fail = c.CreateLabel("fail");

            c.mov(rax, (ulong) (void*) fakeModule);
            c.cmp(rcx, rax);
            c.jnz(fail);

            c.jmp((ulong) (void*) pHook);

            c.Label(ref fail);
            c.jmp((ulong) (void*) pOriginal);

            return c;
        }
    }
}
