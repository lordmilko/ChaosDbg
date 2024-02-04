using System.Diagnostics;
using static ChaosDbg.Analysis.x64Byte;

namespace ChaosDbg.Analysis
{
    [DebuggerDisplay("{Name} ({Value,nq})")]
    class InstrBytes
    {
        public static readonly InstrBytes Int3 = new InstrBytes("int 3", $"0x{x64Byte.Int3}");

        public static readonly InstrBytes LeaRbpRsp = new InstrBytes("lea rbp, [rsp]", $"0x{x64Byte.LeaRbpRsp}");

        //This value matches the bytes [48/4C] 8B [C4/CC/D4/DC/E4/EC/F4/FC]
        public static readonly InstrBytes MovAnyRsp = new InstrBytes("mov ?, rsp", x64Byte.MovAnyRsp);

        //This value matches the bytes 48 8B C4
        public static readonly InstrBytes MovRaxRsp = new InstrBytes("mov rax, rsp", $"0x{x64Byte.MovRaxRsp}");

        //This value matches the bytes 48 8B [E8/E9/EA/EB/EC/ED/EE/EF]
        public static readonly InstrBytes MovRbpAny = new InstrBytes("mov rbp, ?", $"0x{x64Byte.MovRbpAny}");

        public static readonly InstrBytes Mov_RspDisp_AnyReg = new InstrBytes("mov [rsp+#], ?", $"0x{Mov_RspDisp8_Any}");

        public static readonly InstrBytes Mov_RspDisp_AnyReg_AnyDisp = new InstrBytes("mov [rsp+?], ?", $"0x{Mov_RspDisp8_Any_AnyDisp}");

        public static readonly InstrBytes Mov_RspDisp_AnyReg_ArgDisp = new InstrBytes("mov [rsp+arg], ?", $"0x{Mov_RspDisp8_Any_ArgDisp}");

        /// <summary>
        /// nop<para/>
        ///
        /// This value matches the bytes 0x90
        /// </summary>
        public static readonly InstrBytes Nop = new InstrBytes("nop", $"0x{x64Byte.Nop}");

        public static readonly InstrBytes PushAny = new InstrBytes("push ?", $"{x64Byte.PushAny_50}");

        public static readonly InstrBytes PushRbp = new InstrBytes("push rbp", $"0x{PushRbp_55}");

        public static readonly InstrBytes PushRbpAlt = new InstrBytes("push rbp (alt)", $"0x{PushRbp_FFF5}");

        public static readonly InstrBytes Ret = new InstrBytes("ret", $"0x{x64Byte.Ret}");

        //0x4089
        public static readonly InstrBytes RexMov = new InstrBytes("(rex) mov", $"0x{Rex}{OpCode.Mov_89}");

        //0x4889
        public static readonly InstrBytes RexWMov = new InstrBytes("(rex W) mov", $"0x{RexW}{OpCode.Mov_89}");

        //This value matches [40/41] [50/51/52/53/54/55/56/57]
        //Rex will be 41 when the operand is a new 64-bit register
        //Strictly speaking this is RexB not Rex
        public static readonly InstrBytes RexPushAny = new InstrBytes("(rex) push ?", $"{Rex_AnyB} {PushAny_50}");

        //40 55
        public static readonly InstrBytes RexPushRbp = new InstrBytes("(rex) push rbp", $"0x{Rex}{PushRbp_55}");

        //01001.0. 0x89
        public static readonly InstrBytes RexW_AnyB_AnyR_Mov = new InstrBytes("(rex W[R][B]) mov", $"{RexW_AnyB_AnyR} 0x{OpCode.Mov_89}");

        //01001.01 0x89
        public static readonly InstrBytes RexWB_AnyR_Mov = new InstrBytes("(rex W[R][B]) mov", $"{RexWB_AnyR} 0x{OpCode.Mov_89}");

        /// <summary>
        /// sub rsp, #<para/>
        ///
        /// This value matches the bytes 48 83 EC
        /// </summary>
        public static readonly InstrBytes Sub_Rsp_Any = new InstrBytes("sub rsp, ?", $"0x{SubRspImm8}");

        public string Name { get; }

        public string Value { get; }

        public InstrBytes(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public override string ToString()
        {
            return Value;
        }
    }
}
