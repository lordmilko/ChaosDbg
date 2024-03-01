using System;
using ClrDebug;
using Iced.Intel;

namespace ChaosDbg
{
    public static class RegisterContextExtensions
    {
        #region x86

        public static int GetRegisterValue(this in X86_CONTEXT context, Register register)
        {
            var fullRegister = register.GetFullRegister();

            var result = GetFullRegisterValue(context, fullRegister);

            if (fullRegister != register)
            {
                var size = register.GetSize();

                switch (size)
                {
                    case 2:
                        return result & 0x0000FFFF;

                    default:
                        throw new NotImplementedException($"Don't know how to handle a value of size {size}");
                }
            }

            return result;
        }

        private static int GetFullRegisterValue(in X86_CONTEXT context, Register register)
        {
            switch (register)
            {
                // Retrieved by CONTEXT_DEBUG_REGISTERS
                case Register.DR0:
                    return context.Dr0;

                case Register.DR1:
                    return context.Dr1;

                case Register.DR2:
                    return context.Dr2;

                case Register.DR3:
                    return context.Dr3;

                case Register.DR6:
                    return context.Dr6;

                case Register.DR7:
                    return context.Dr7;

                /*
                    // Retrieved by CONTEXT_FLOATING_POINT
                    public X86_FLOATING_SAVE_AREA FloatSave;
                */

                // Retrieved by CONTEXT_SEGMENTS

                case Register.GS:
                    return context.SegFs;

                case Register.FS:
                    return context.SegFs;

                case Register.ES:
                    return context.SegEs;

                case Register.DS:
                    return context.SegDs;

                // Retrieved by CONTEXT_INTEGER

                case Register.EDI:
                    return context.Edi;

                case Register.ESI:
                    return context.Esi;

                case Register.EBX:
                    return context.Ebx;

                case Register.EDX:
                    return context.Edx;

                case Register.ECX:
                    return context.Ecx;

                case Register.EAX:
                    return context.Eax;

                // Retrieved by CONTEXT_CONTROL

                case Register.EBP:
                    return context.Ebp;

                case Register.EIP:
                    return context.Eip;

                case Register.CS:
                    return context.SegCs;

                /* X86_CONTEXT_FLAGS EFlags */

                case Register.ESP:
                    return context.Esp;

                case Register.SS:
                    return context.SegSs;

                /*
                    Retrieved by CONTEXT_EXTENDED_REGISTERS
                    ExtendedRegisters[512];
                */

                default:
                    throw new NotImplementedException($"Don't know how to get register {register}");
            }
        }

        #endregion
        #region x64

        public static long GetRegisterValue(this in AMD64_CONTEXT context, Register register)
        {
            var fullRegister = register.GetFullRegister();

            var result = GetFullRegisterValue(context, fullRegister);

            if (fullRegister != register)
            {
                var size = register.GetSize();

                switch (size)
                {
                    case 2:
                        return result & 0x000000000000FFFF;

                    case 4:
                        return result & 0x00000000FFFFFFFF;

                    default:
                        throw new NotImplementedException($"Don't know how to handle a value of size {size}");
                }
            }

            return result;
        }

        private static long GetFullRegisterValue(in AMD64_CONTEXT context, Register register)
        {
            switch (register)
            {
                /*
                    P1Home;
                    P2Home;
                    P3Home;
                    P4Home;
                    P5Home;
                    P6Home;

                    // Control Flags
                    public int MxCsr;

                    // Segment Registers and Processor Flags
                    public X86_CONTEXT_FLAGS EFlags;
                 */

                case Register.CS:
                    return context.SegCs;

                case Register.DS:
                    return context.SegDs;

                case Register.ES:
                    return context.SegEs;

                case Register.FS:
                    return context.SegFs;

                case Register.GS:
                    return context.SegFs;

                case Register.SS:
                    return context.SegSs;

                // Debug Registers
                case Register.DR0:
                    return context.Dr0;

                case Register.DR1:
                    return context.Dr1;

                case Register.DR2:
                    return context.Dr2;

                case Register.DR3:
                    return context.Dr3;

                case Register.DR6:
                    return context.Dr6;

                case Register.DR7:
                    return context.Dr7;

                // Integer Registers
                case Register.RAX:
                    return context.Rax;

                case Register.RCX:
                    return context.Rcx;

                case Register.RDX:
                    return context.Rdx;

                case Register.RBX:
                    return context.Rbx;

                case Register.RSP:
                    return context.Rsp;

                case Register.RBP:
                    return context.Rbp;

                case Register.RSI:
                    return context.Rsi;

                case Register.RDI:
                    return context.Rdi;

                case Register.R8:
                    return context.R8;

                case Register.R9:
                    return context.R9;

                case Register.R10:
                    return context.R10;

                case Register.R11:
                    return context.R11;

                case Register.R12:
                    return context.R12;

                case Register.R13:
                    return context.R13;

                case Register.R14:
                    return context.R14;

                case Register.R15:
                    return context.R15;

                case Register.RIP:
                    return context.Rip;

                /*
                    // Floating Point State
                    FltSave;
                    Legacy;
                 */

                case Register.XMM0:
                    return context.Xmm0;

                case Register.XMM1:
                    return context.Xmm1;

                case Register.XMM2:
                    return context.Xmm2;

                case Register.XMM3:
                    return context.Xmm3;

                case Register.XMM4:
                    return context.Xmm4;

                case Register.XMM5:
                    return context.Xmm5;

                case Register.XMM6:
                    return context.Xmm6;

                case Register.XMM7:
                    return context.Xmm7;

                case Register.XMM8:
                    return context.Xmm8;

                case Register.XMM9:
                    return context.Xmm9;

                case Register.XMM10:
                    return context.Xmm10;

                case Register.XMM11:
                    return context.Xmm11;

                case Register.XMM12:
                    return context.Xmm12;

                case Register.XMM13:
                    return context.Xmm13;

                case Register.XMM14:
                    return context.Xmm14;

                case Register.XMM15:
                    return context.Xmm15;

                /*
                    // Vector Registers
                    VectorRegister;
                    VectorControl;

                    // Special Debug Control Registers
                    DebugControl;
                    LastBranchToRip;
                    LastBranchFromRip;
                    LastExceptionToRip;
                    LastExceptionFromRip;
                 */

                default:
                    throw new NotImplementedException($"Don't know how to get register {register}");
            }
        }

        #endregion
    }
}
