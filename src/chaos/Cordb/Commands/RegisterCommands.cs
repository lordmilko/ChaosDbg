using System;
using System.Linq;
using ChaosDbg.Cordb;
using ClrDebug;

namespace chaos.Cordb.Commands
{
    class RegisterCommands
    {
        private CordbEngine engine;

        public RegisterCommands(CordbEngine engine)
        {
            this.engine = engine;
        }

        [Command("r")]
        public void Register()
        {
            var frame = engine.Process.Threads.ActiveThread.StackTrace.First();

            var context = frame.Context;

            if (context.IsAmd64)
            {
                var amd64 = context.Raw.Amd64Context;

                Console.WriteLine($"rax={amd64.Rax:x16} rbx={amd64.Rbx:x16} rcx={amd64.Rcx:x16}");
                Console.WriteLine($"rdx={amd64.Rdx:x16} rsi={amd64.Rsi:x16} rdi={amd64.Rdi:x16}");
                Console.WriteLine($"rip={amd64.Rip:x16} rsp={amd64.Rsp:x16} rbp={amd64.Rbp:x16}");
                Console.WriteLine($" r8={amd64.R8:x16}  r9={amd64.R9:x16} r10={amd64.R10:x16}");
                Console.WriteLine($"r11={amd64.R11:x16} r12={amd64.R12:x16} r13={amd64.R13:x16}");
                Console.WriteLine($"r14={amd64.R14:x16} r15={amd64.R15:x16}");

                var eflags = amd64.EFlags;

                Console.WriteLine(
                    "iopl={0:x1} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10}",
                    ((int) eflags >> 12) & 3,
                    eflags.HasFlag(X86_CONTEXT_FLAGS.VIP) ? "vip" : "   ",
                    eflags.HasFlag(X86_CONTEXT_FLAGS.VIF) ? "vif" : "   ",
                    eflags.HasFlag(X86_CONTEXT_FLAGS.OF) ? "ov" : "nv",
                    eflags.HasFlag(X86_CONTEXT_FLAGS.DF) ? "dn" : "up",
                    eflags.HasFlag(X86_CONTEXT_FLAGS.IF) ? "ei" : "di",
                    eflags.HasFlag(X86_CONTEXT_FLAGS.SF) ? "ng" : "pl",
                    eflags.HasFlag(X86_CONTEXT_FLAGS.ZF) ? "zr" : "nz",
                    eflags.HasFlag(X86_CONTEXT_FLAGS.AF) ? "ac" : "na",
                    eflags.HasFlag(X86_CONTEXT_FLAGS.PF) ? "po" : "pe",
                    eflags.HasFlag(X86_CONTEXT_FLAGS.CF) ? "cy" : "nc"
                );
                Console.WriteLine($"cs={amd64.SegCs:x4}  ss={amd64.SegSs:x4}  ds={amd64.SegDs:x4}  es={amd64.SegEs:x4}  fs={amd64.SegFs:x4}  gs={amd64.SegGs:x4}             efl={(int)amd64.EFlags:x8}");
            }
            else
            {
                var x86 = context.Raw.X86Context;

                Console.WriteLine($"eax={x86.Eax:x8} ebx={x86.Ebx:x8} ecx={x86.Ecx:x8} edx={x86.Edx:x8} esi={x86.Esi:x8} edi={x86.Edi:x8}");

                var eflags = x86.EFlags;

                Console.Write($"eip={x86.Eip:x8} esp={x86.Esp:x8} ebp={x86.Ebp:x8} ");
                Console.WriteLine(
                    "iopl={0:x1} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10}",
                    ((int) eflags >> 12) & 3,
                    eflags.HasFlag(X86_CONTEXT_FLAGS.VIP) ? "vip" : "   ",
                    eflags.HasFlag(X86_CONTEXT_FLAGS.VIF) ? "vif" : "   ",
                    eflags.HasFlag(X86_CONTEXT_FLAGS.OF) ? "ov" : "nv",
                    eflags.HasFlag(X86_CONTEXT_FLAGS.DF) ? "dn" : "up",
                    eflags.HasFlag(X86_CONTEXT_FLAGS.IF) ? "ei" : "di",
                    eflags.HasFlag(X86_CONTEXT_FLAGS.SF) ? "ng" : "pl",
                    eflags.HasFlag(X86_CONTEXT_FLAGS.ZF) ? "zr" : "nz",
                    eflags.HasFlag(X86_CONTEXT_FLAGS.AF) ? "ac" : "na",
                    eflags.HasFlag(X86_CONTEXT_FLAGS.PF) ? "po" : "pe",
                    eflags.HasFlag(X86_CONTEXT_FLAGS.CF) ? "cy" : "nc"
                );

                Console.WriteLine($"cs={x86.SegCs:x4}  ss={x86.SegSs:x4}  ds={x86.SegDs:x4}  es={x86.SegEs:x4}  fs={x86.SegFs:x4}  gs={x86.SegGs:x4}             efl={(int)eflags:x8}");
            }

            Console.WriteLine(frame + ":");

            Console.WriteLine("disasm");
        }
    }
}
