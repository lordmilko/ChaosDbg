using static ChaosDbg.Analysis.x64Byte.Disp;
using static ChaosDbg.Analysis.x64Byte.OpCode;
using static ChaosDbg.Analysis.x64Byte.SIB;
using static ChaosDbg.Analysis.x64Byte.ModRM;

namespace ChaosDbg.Analysis
{
    static class x64Byte
    {
        #region Prefix

        /// <summary>
        /// Specifies that the instruction utilizes x64 specific operands but there's no modifications
        /// to ModR/M that should be made (and there probably isn't even any operands)
        /// </summary>
        public const string Rex = "40";

        //This value matches [40/41]
        public const string Rex_AnyB = "0100000.";

        /// <summary>
        /// Specifies that the instruction utilizes x64 specific operands and that the operand size is 64-bits.
        /// </summary>
        public const string RexW = "48";

        /// <summary>
        /// Specifies that the instruction utilizes x64 specific operands, the operand size is 64-bits
        /// and that the Reg field of ModR/M accesses a new 64-bit specific register.
        /// </summary>
        public const string RexWR = "4C";

        //This value matches [48/4C]
        public const string RexW_AnyR = "01001.00";

        public const string RexWB_AnyR = "01001.01";

        /// <summary>
        /// Specifies that the instruction utilizes x64 specific operands, the operand size is 64-bits,
        /// the R/M field of ModR/M accesses a new 64-bit register and that the Reg field of ModR/M may or may not
        /// access a new 64-bit register as well.
        ///
        /// This value matches [48/49/4C/4D]
        /// </summary>
        public const string RexW_AnyB_AnyR = "01001.0.";

        #endregion

        #region Instruction

        //Complete instructions (possibly derived from smaller byte components)

        public const string Int3 = "CC";

        public const string Nop = "90";

        //push r64. push is 50 + the register you want to push. 5 = 101 which is rbp (as seen below in the ModR/M region)
        public const string PushRbp_55 = "55";

        //push is 50 + the register you want to push. 50 is 0101. The register only requires 3 bits,
        //which means 0101 0 are filled in and the last 3 digits are question marks
        public const string PushAny_50 = "01010...";

        //push r/m64
        public const string PushRbp_FFF5 = "FFF5";

        public const string Ret = "C3";

        //TEMP
        public const string SubImm8 = Sub_83 + Imm8Rsp;

        /// <summary>
        /// sub rsp, #<para/>
        ///
        /// This value matches the bytes 48 83 EC<para/>
        ///
        /// Technically speaking, the fact an imm8 will exist is recorded in ModR/M before
        /// rsp is, but thats not how it reads in the end.<para/>
        /// 
        /// Has always been observed as having RexW.<para/>
        /// 
        /// The numeric immediate follows the end of the pattern.
        /// </summary>
        public const string SubRspImm8 = RexW + Sub_83 + Imm8Rsp;

        /// <summary>
        /// mov ?, rsp<para/>
        ///
        /// 01001.00 0x8B 11...100
        ///
        /// This value matches the bytes [48/4C] 8B [C4/CC/D4/DC/E4/EC/F4/FC]<para/>
        ///
        /// If ? is a new x64 bit register, REX will be 4C. Otherwise, it will be 48. REX.B will never be set since
        /// the source register is always rsp.
        /// </summary>
        public const string MovAnyRsp = RexW_AnyR + " 0x" + Mov_8B + " " + AnyRsp;

        /// <summary>
        /// mov rax, rsp<para/>
        ///
        /// This value matches the bytes 48 8B C4<para/>
        /// 
        /// Has always been observed as having RexW.
        /// </summary>
        public const string MovRaxRsp = RexW + Mov_8B + RaxRsp;

        /// <summary>
        /// mov rbp, ?<para/>
        ///
        /// 0x488B 11101...
        /// 
        /// This value matches the bytes 48 8B [E8/E9/EA/EB/EC/ED/EE/EF]<para/>
        /// 
        /// Has always been observed as having RexW.
        /// </summary>
        public const string MovRbpAny = RexW + Mov_8B + " " + RbpAny;

        /// <summary>
        /// mov [rsp+#], ?<para/>
        ///
        /// 0x4889 01...100 ..100100<para/>
        /// 
        /// This value matches the bytes 48 89 [44/4C/54/5C/64/6C/74/7C][24/64/A4/E4]
        ///
        /// Has always been observed as having RexW.<para/>
        ///
        /// The value of ? will plug into the Reg field of ModR/M. As such, ? will implicitly always be another register.
        /// </summary>
        public const string Mov_RspDisp8_Any = RexW + Mov_89 + " " + RspDisp8Any;

        public const string Mov_RspDisp8_Any_AnyDisp = Mov_RspDisp8_Any + " " + DispAny;

        public const string Mov_RspDisp8_Any_ArgDisp = Mov_RspDisp8_Any + " " + AnyDiv4;

        /// <summary>
        /// lea rbp, [rsp]<para/>
        ///
        /// Has always been observed as having RexW.
        /// </summary>
        public const string LeaRbpRsp = RexW + Lea + EffRbpRsp;

        #endregion

        public static class OpCode
        {
            //Opcode identifiers that require ModR/M (and possibly also SIB + displacement bytes)

            public const string Lea = "8D";

            /// <summary>
            /// Represents a normal mov instruction that moves from a register or memory location to another register.
            /// e.g. mov rax,[0x1234]. An instruction mov A,B can be encoded using 89 instead with the operands in ModR/M
            /// flipped around.
            /// </summary>
            public const string Mov_8B = "8B";

            /// <summary>
            /// Represents a reverse mov instruction that moves from one register to another register or memory location.
            /// e.g. mov [0x12345],rax. An instruction mov B,A can be encoded using 8B instead with the operands in ModR/M
            /// flipped around.
            /// </summary>
            public const string Mov_89 = "89";

            //83 is one of the operands for sub.
            public const string Sub_83 = "83";
        }

        public static class ModRM
        {
            /* ModR/M modes that affect how the Reg and R/M fields are interpreted

             *   00 - R/M stores effective address. No displacement, unless R/M is 110, in which case effective address is a 16-bit displacement that follows the ModR/M byte
             *   01 - effective address is the sum of R/M + an 8-bit displacement that follows the ModR/M byte
             *   10 - effective address is the sum of R/M + a 16-bit displacement that follows the ModR/M byte
             *   11 - R/M is another register operand
             *
             * ModR/M register encodings for Reg and R/M. Alternate values are present when REX.R=1 or REX.B=1
             * (depending on whether the register is sitting in Reg or R/M)
             *
             *   000 rax/r8
             *   001 rcx/r9
             *   010 rdx/r10
             *   011 rbx/r11
             *   100 rsp/r12
             *   101 rbp/r13
             *   110 rsi/r14
             *   111 rdi/r15
             */

            /// <summary>
            /// rax (Reg), rsp (R/M)<para/>
            ///
            /// ModR/M:
            /// 
            /// Mode      Reg        R/M
            /// 11 (R2R)  000 (rax)  100 (rsp)
            /// </summary>
            public const string RaxRsp = "C4";

            /// <summary>
            /// rbp (Reg), ? (R/M)<para/>
            ///
            /// This value matches the byte [E8/E9/EA/EB/EC/ED/EE/EF]
            /// 
            /// ModR/M:
            ///
            /// Mode      Reg        R/M
            /// 11 (R2R)  101 (rbp)  ... (any)
            /// </summary>
            public const string RbpAny = "11101...";

            /// <summary>
            /// ? (Reg), rsp (R/M)
            ///
            /// This value matches the byte [C4/CC/D4/DC/E4/EC/F4/FC]
            /// 
            /// ModR/M:
            ///
            /// Mode      Reg   R/M
            /// 11 (R2R)  ?     100 (rsp)
            /// </summary>
            public const string AnyRsp = "11...100";

            /// <summary>
            /// [SIB+#], ?<para/>
            ///
            /// This value matches the byte [44/4C/54/5C/64/6C/74/7C]
            /// 
            /// An unspecified register will interact with the value pointed to by SIB + an 8-bit displacement (to be defined in a subsequent byte).
            /// When the mode is 01. Normally rsp would be represented by value 100, however R/M being 100 has special meaning to signify that a SIB byte
            /// should be present. If the SIB base is 100, that will mean that in fact its going tp be rsp + an 8-bit displacement.<para/>
            ///
            /// Implicitly because Reg is left unspecified, whatever the value of ? is will be another register.
            ///
            /// ModR/M:
            ///
            /// Mode         Reg   R/M
            /// 01 (Disp8)   ?     100 (not rsp, but in fact a flag that says to check SIB)
            ///
            /// e.g. if we have mov [rsp+10], rbx this pattern will match 01011100 (011 is rbx)
            /// </summary>
            public const string SibDisp8Any = "01...100";

            /// <summary>
            /// [rsp+#], ?<para/>
            ///
            /// 01...100 ..100100<para/>
            ///
            /// This value matches the bytes [44/4C/54/5C/64/6C/74/7C][24/64/A4/E4]
            ///
            /// The common case of having a memory displacement from rsp. Since R/M 100 means you need to check the SIB byte,
            /// we declare this constant for handling the common case of having [rsp+#], ?
            /// </summary>
            public const string RspDisp8Any = SibDisp8Any + SibAnyRsp;

            /// <summary>
            /// rbp, [SIB}
            ///
            /// ModR/M:
            ///
            /// Mode            Reg        R/M
            /// 00 (Eff. Addr)  101 (rbp)  100 (SIB)
            /// </summary>
            public const string EffRbpSib = "2C";

            public const string EffRbpRsp = EffRbpSib + SibRsp;

            //EC is a ModR/M of 11 101 100.
            //Operations on immediate values also use mode 11 (register-to-register). Sub
            //interprets the value of Reg (101) as an 8-bit sign extended vaue. 100 is rsp
            public const string Imm8Rsp = "EC";
        }

        public static class SIB
        {
            /* The SIB byte is used to help out when you've operand that needs to reference a memory address in a complex way. A SIB byte is present
             * when the R/M mode is 00, 01 or 10 and the R/M field is 100.
             *
             * SIB has 3 fields
             *
             * Scale | Index | Base
             *
             * In most cases, scale will be 0 and index will be 100 (rsp again, meaning it has no value). Base will then contain the true register to use in
             * the memory operation. Thus, if we have a Mod/RM of
             *
             * Mode          Reg   R/M
             *  011 (Disp8)  rbx   100 (check SIB)
             *
             * and a SIB of
             *
             * Scale   Index              Base
             *    00   100 (rsp = none)   100 (rsp)
             *
             * our end result is [rsp+displacement], rbx
             *
             * Where Scale and Index come into play is when you have more complex offsets. e.g. if SIB was instead
             *
             * Scale   Index       Base
             *    10   101 (rbp)   100 (rsp)
             *
             * The offset instead becomes
             *   Base + (Index * Scale) + displacement
             *
             * and so, the final instruction would become
             *
             *   [rsp + (rbp * 2) + displacement]
             */

            /// <summary>
            /// A SIB byte with an unspecified scale and default index (none) that operates against rsp<para/>
            ///
            /// This value matches the byte [24/64/A4/E4]
            /// </summary>
            public const string SibAnyRsp = "..100100";

            /// <summary>
            /// The SIB byte identifies rsp exactly, with no additional "+index*scale" business necessary.
            ///
            /// Scale   Index              Base
            ///    00   100 (rsp = none)   100 (rsp)
            /// 
            /// </summary>
            public const string SibRsp = "24";
        }

        public static class Disp
        {
            /// <summary>
            /// Represents a displacement that matches any number that is divisible by 4. Arguments to a function will be stored at [rsp+displacement]
            /// which will always be some increasing multiple of 4 (actually on x64 they'd be a multiple of 8)
            /// </summary>
            public const string AnyDiv4 = "......00";

            public const string DispAny = "0x..";
        }
    }
}
