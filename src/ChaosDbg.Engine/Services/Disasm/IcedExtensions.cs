using System;
using ClrDebug;
using Iced.Intel;

namespace ChaosDbg.Disasm
{
    internal static class IcedExtensions
    {
        public static Register ToIcedRegister(this CorDebugRegister register, IMAGE_FILE_MACHINE arch)
        {
            //Each CorDebugRegister can have multiple values associated with a given value, the meaning
            //of which is dependent upon what architecture the register is supposed to be interpreted as.
            //This is causes big problems for us when we want to try and display a register. Iced Register
            //values have a unique value for each enum member, which makes things a lot more coherent

            switch (arch)
            {
                case IMAGE_FILE_MACHINE.I386:
                    return ToIcedRegisterX86(register);

                case IMAGE_FILE_MACHINE.AMD64:
                    return ToIcedRegisterX64(register);

                default:
                    throw new NotImplementedException($"Don't know how to handle {nameof(IMAGE_FILE_MACHINE)} '{arch}'.");
            }
        }

        private static Register ToIcedRegisterX86(CorDebugRegister register)
        {
            return register switch
            {
                CorDebugRegister.REGISTER_X86_EIP => Register.EIP,
                CorDebugRegister.REGISTER_X86_ESP => Register.ESP,
                CorDebugRegister.REGISTER_X86_EBP => Register.EBP,
                CorDebugRegister.REGISTER_X86_EAX => Register.EAX,
                CorDebugRegister.REGISTER_X86_ECX => Register.ECX,
                CorDebugRegister.REGISTER_X86_EDX => Register.EDX,
                CorDebugRegister.REGISTER_X86_EBX => Register.EBX,
                CorDebugRegister.REGISTER_X86_ESI => Register.ESI,
                CorDebugRegister.REGISTER_X86_EDI => Register.EDI,
                CorDebugRegister.REGISTER_X86_FPSTACK_0 => Register.ST0,
                CorDebugRegister.REGISTER_X86_FPSTACK_1 => Register.ST1,
                CorDebugRegister.REGISTER_X86_FPSTACK_2 => Register.ST2,
                CorDebugRegister.REGISTER_X86_FPSTACK_3 => Register.ST3,
                CorDebugRegister.REGISTER_X86_FPSTACK_4 => Register.ST4,
                CorDebugRegister.REGISTER_X86_FPSTACK_5 => Register.ST5,
                CorDebugRegister.REGISTER_X86_FPSTACK_6 => Register.ST6,
                CorDebugRegister.REGISTER_X86_FPSTACK_7 => Register.ST7,
                _ => throw new ArgumentOutOfRangeException(nameof(register), register, null)
            };
        }

        private static Register ToIcedRegisterX64(CorDebugRegister register)
        {
            return register switch
            {
                CorDebugRegister.REGISTER_AMD64_RIP => Register.RIP,
                CorDebugRegister.REGISTER_AMD64_RSP => Register.RSP,
                CorDebugRegister.REGISTER_AMD64_RBP => Register.RBP,
                CorDebugRegister.REGISTER_AMD64_RAX => Register.RAX,
                CorDebugRegister.REGISTER_AMD64_RCX => Register.RCX,
                CorDebugRegister.REGISTER_AMD64_RDX => Register.RDX,
                CorDebugRegister.REGISTER_AMD64_RBX => Register.RBX,
                CorDebugRegister.REGISTER_AMD64_RSI => Register.RSI,
                CorDebugRegister.REGISTER_AMD64_RDI => Register.RDI,
                CorDebugRegister.REGISTER_AMD64_R8 => Register.R8,
                CorDebugRegister.REGISTER_AMD64_R9 => Register.R9,
                CorDebugRegister.REGISTER_AMD64_R10 => Register.R10,
                CorDebugRegister.REGISTER_AMD64_R11 => Register.R11,
                CorDebugRegister.REGISTER_AMD64_R12 => Register.R12,
                CorDebugRegister.REGISTER_AMD64_R13 => Register.R13,
                CorDebugRegister.REGISTER_AMD64_R14 => Register.R14,
                CorDebugRegister.REGISTER_AMD64_R15 => Register.R15,
                CorDebugRegister.REGISTER_AMD64_XMM0 => Register.XMM0,
                CorDebugRegister.REGISTER_AMD64_XMM1 => Register.XMM1,
                CorDebugRegister.REGISTER_AMD64_XMM2 => Register.XMM2,
                CorDebugRegister.REGISTER_AMD64_XMM3 => Register.XMM3,
                CorDebugRegister.REGISTER_AMD64_XMM4 => Register.XMM4,
                CorDebugRegister.REGISTER_AMD64_XMM5 => Register.XMM5,
                CorDebugRegister.REGISTER_AMD64_XMM6 => Register.XMM6,
                CorDebugRegister.REGISTER_AMD64_XMM7 => Register.XMM7,
                CorDebugRegister.REGISTER_AMD64_XMM8 => Register.XMM8,
                CorDebugRegister.REGISTER_AMD64_XMM9 => Register.XMM9,
                CorDebugRegister.REGISTER_AMD64_XMM10 => Register.XMM10,
                CorDebugRegister.REGISTER_AMD64_XMM11 => Register.XMM11,
                CorDebugRegister.REGISTER_AMD64_XMM12 => Register.XMM12,
                CorDebugRegister.REGISTER_AMD64_XMM13 => Register.XMM13,
                CorDebugRegister.REGISTER_AMD64_XMM14 => Register.XMM14,
                CorDebugRegister.REGISTER_AMD64_XMM15 => Register.XMM15,
                _ => throw new ArgumentOutOfRangeException(nameof(register), register, null)
            };
        }
    }
}
