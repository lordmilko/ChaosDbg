using System;
using ClrDebug;
using Iced.Intel;

namespace ChaosDbg
{
    public static class RegisterContextExtensions
    {
        public static long GetRegisterValue(this CrossPlatformContext context, Register register)
        {
            if (context.IsX86)
                return context.Raw.X86Context.GetRegisterValue(register);

            if (context.IsAmd64)
                return context.Raw.Amd64Context.GetRegisterValue(register);

            throw new NotImplementedException($"Don't know what CPU architecture the register context is (flags: {context.Flags})");
        }

        public static void SetRegisterValue(this CrossPlatformContext context, Register register, long value)
        {
            if (context.IsX86)
            {
                context.Raw.X86Context.SetRegisterValue(register, (int) value);
                return;
            }   

            if (context.IsAmd64)
            {
                context.Raw.Amd64Context.SetRegisterValue(register, value);
                return;
            }

            throw new NotImplementedException($"Don't know what CPU architecture the register context is (flags: {context.Flags})");
        }

        #region x86
        #region Get

        public static int GetRegisterValue(this in X86_CONTEXT context, Register register)
        {
            var fullRegister = register.GetFullRegister32();

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
        #region Set

        public static void SetRegisterValue(this ref X86_CONTEXT context, Register register, int value)
        {
            var fullRegister = register.GetFullRegister32();

            if (fullRegister != register)
                throw new NotImplementedException("Setting partial register values is not implemented. Not sure if we need to merge it with the full value or something");

            SetFullRegisterValue(ref context, fullRegister, value);
        }

        private static void SetFullRegisterValue(ref X86_CONTEXT context, Register register, int value)
        {
            switch (register)
            {
                // Retrieved by CONTEXT_DEBUG_REGISTERS
                case Register.DR0:
                    context.Dr0 = value;
                    break;
                case Register.DR1:
                    context.Dr1 = value;
                    break;
                case Register.DR2:
                    context.Dr2 = value;
                    break;
                case Register.DR3:
                    context.Dr3 = value;
                    break;
                case Register.DR6:
                    context.Dr6 = value;
                    break;
                case Register.DR7:
                    context.Dr7 = value;
                    break;
                /*
                    // Retrieved by CONTEXT_FLOATING_POINT
                    public X86_FLOATING_SAVE_AREA FloatSave;
                */

                // Retrieved by CONTEXT_SEGMENTS

                case Register.GS:
                    context.SegFs = value;
                    break;
                case Register.FS:
                    context.SegFs = value;
                    break;
                case Register.ES:
                    context.SegEs = value;
                    break;
                case Register.DS:
                    context.SegDs = value;
                    break;

                // Retrieved by CONTEXT_INTEGER

                case Register.EDI:
                    context.Edi = value;
                    break;
                case Register.ESI:
                    context.Esi = value;
                    break;
                case Register.EBX:
                    context.Ebx = value;
                    break;
                case Register.EDX:
                    context.Edx = value;
                    break;
                case Register.ECX:
                    context.Ecx = value;
                    break;
                case Register.EAX:
                    context.Eax = value;
                    break;

                // Retrieved by CONTEXT_CONTROL

                case Register.EBP:
                    context.Ebp = value;
                    break;
                case Register.EIP:
                    context.Eip = value;
                    break;
                case Register.CS:
                    context.SegCs = value;
                    break;

                /* X86_CONTEXT_FLAGS EFlags */

                case Register.ESP:
                    context.Esp = value;
                    break;
                case Register.SS:
                    context.SegSs = value;
                    break;
                /*
                    Retrieved by CONTEXT_EXTENDED_REGISTERS
                    ExtendedRegisters[512];
                */

                default:
                    throw new NotImplementedException($"Don't know how to get register {register}");
            }
        }

        #endregion
        #endregion
        #region x64
        #region Get

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
        #region Set

        public static void SetRegisterValue(this ref AMD64_CONTEXT context, Register register, long value)
        {
            var fullRegister = register.GetFullRegister();

            if (fullRegister != register)
                throw new NotImplementedException("Setting partial register values is not implemented. Not sure if we need to merge it with the full value or something");

            SetFullRegisterValue(ref context, fullRegister, value);
        }

        private static void SetFullRegisterValue(ref AMD64_CONTEXT context, Register register, long value)
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
                    context.SegCs = (ushort) value;
                    break;

                case Register.DS:
                    context.SegDs = (ushort) value;
                    break;

                case Register.ES:
                    context.SegEs = (ushort) value;
                    break;

                case Register.FS:
                    context.SegFs = (ushort) value;
                    break;

                case Register.GS:
                    context.SegFs = (ushort) value;
                    break;

                case Register.SS:
                    context.SegSs = (ushort) value;
                    break;

                // Debug Registers
                case Register.DR0:
                    context.Dr0 = value;
                    break;

                case Register.DR1:
                    context.Dr1 = value;
                    break;

                case Register.DR2:
                    context.Dr2 = value;
                    break;

                case Register.DR3:
                    context.Dr3 = value;
                    break;

                case Register.DR6:
                    context.Dr6 = value;
                    break;

                case Register.DR7:
                    context.Dr7 = value;
                    break;

                // Integer Registers
                case Register.RAX:
                    context.Rax = value;
                    break;

                case Register.RCX:
                    context.Rcx = value;
                    break;

                case Register.RDX:
                    context.Rdx = value;
                    break;

                case Register.RBX:
                    context.Rbx = value;
                    break;

                case Register.RSP:
                    context.Rsp = value;
                    break;

                case Register.RBP:
                    context.Rbp = value;
                    break;

                case Register.RSI:
                    context.Rsi = value;
                    break;

                case Register.RDI:
                    context.Rdi = value;
                    break;

                case Register.R8:
                    context.R8 = value;
                    break;

                case Register.R9:
                    context.R9 = value;
                    break;

                case Register.R10:
                    context.R10 = value;
                    break;

                case Register.R11:
                    context.R11 = value;
                    break;

                case Register.R12:
                    context.R12 = value;
                    break;

                case Register.R13:
                    context.R13 = value;
                    break;

                case Register.R14:
                    context.R14 = value;
                    break;

                case Register.R15:
                    context.R15 = value;
                    break;

                case Register.RIP:
                    context.Rip = value;
                    break;

                /*
                    // Floating Point State
                    FltSave;
                    Legacy;
                 */

                case Register.XMM0:
                    context.Xmm0 = value;
                    break;

                case Register.XMM1:
                    context.Xmm1 = value;
                    break;

                case Register.XMM2:
                    context.Xmm2 = value;
                    break;

                case Register.XMM3:
                    context.Xmm3 = value;
                    break;

                case Register.XMM4:
                    context.Xmm4 = value;
                    break;

                case Register.XMM5:
                    context.Xmm5 = value;
                    break;

                case Register.XMM6:
                    context.Xmm6 = value;
                    break;

                case Register.XMM7:
                    context.Xmm7 = value;
                    break;

                case Register.XMM8:
                    context.Xmm8 = value;
                    break;

                case Register.XMM9:
                    context.Xmm9 = value;
                    break;

                case Register.XMM10:
                    context.Xmm10 = value;
                    break;

                case Register.XMM11:
                    context.Xmm11 = value;
                    break;

                case Register.XMM12:
                    context.Xmm12 = value;
                    break;

                case Register.XMM13:
                    context.Xmm13 = value;
                    break;

                case Register.XMM14:
                    context.Xmm14 = value;
                    break;

                case Register.XMM15:
                    context.Xmm15 = value;
                    break;

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
        #endregion
    }
}
