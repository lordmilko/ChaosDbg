using System;
using System.Collections.Generic;
using System.Linq;
using static ChaosDbg.Analysis.InstrBytes;

namespace ChaosDbg.Analysis
{
    class x64ByteSequence
    {
        [Obsolete("This member is for documentation purposes only")]
        private static ByteSequence[] x64Ghidra =
        {
            new ByteSequence("0x909090 * 0x4883ec"),                                      //NOP : NOP : NOP : SUB RSP, #
            new ByteSequence(Nop, Nop, Nop, "*", Sub_Rsp_Any),

            new ByteSequence("0x909090 * 0x4889 01...100..100100"),                       //NOP : NOP : NOP : MOV [RSP+#],R.
            new ByteSequence(Nop, Nop, Nop, "*", Mov_RspDisp_AnyReg),

            new ByteSequence("0x90 * 0x4889 01...100..100100 0x..4889"),                  //NOP : MOV [RSP+#],R.. MOV [ ],
            new ByteSequence(Nop, "*", Mov_RspDisp_AnyReg_AnyDisp, RexWMov),

            new ByteSequence("0x90 * 0x4889 01...100..100100 ......00 01010...01010..."), //NOP : MOV [RSP+#],R.. PUSH PUSH
            new ByteSequence(Nop, "*", Mov_RspDisp_AnyReg_ArgDisp, PushAny, PushAny),

            new ByteSequence("0xc3 * 0x4889 01...100..100100 ......00 01010...01010..."), //RET : MOV [RSP+#],R.. PUSH PUSH
            new ByteSequence(Ret, "*", Mov_RspDisp_AnyReg_ArgDisp, PushAny, PushAny),

            new ByteSequence("0xc3 * 0x55488d2c24"),                                      //RET : PUSH RBP, LEA RBP,[RSP]
            new ByteSequence(Ret, "*", PushRbp, LeaRbpRsp),

            new ByteSequence("0x90 * 0x55488d2c24"),                                      //NOP : PUSH RBP, LEA RBP,[RSP]
            new ByteSequence(Nop, "*", PushRbp, LeaRbpRsp),

            new ByteSequence("0x90 * 01010... 0x55488d2c24"),                             //NOP : PUSH, PUSH RBP, LEA RBP,[RSP]
            new ByteSequence(Nop, "*", PushAny, PushRbp, LeaRbpRsp),

            new ByteSequence("0xc3 * 01010... 0x55488d2c24"),                             //RET : PUSH, PUSH RBP, LEA RBP,[RSP]
            new ByteSequence(Ret, "*", PushAny, PushRbp, LeaRbpRsp),

            new ByteSequence("0x909090 * 0x488bc4"),                                      //NOP : NOP : NOP : MOV RAX,RSP
            new ByteSequence(Nop, Nop, Nop, "*", MovRaxRsp),

            new ByteSequence("0x90 * 0xfff54883ec"),                                      //NOP : PUSH RBP : SUB RSP, #
            new ByteSequence(Nop, "*", PushRbpAlt, Sub_Rsp_Any),

            new ByteSequence("0x90 * 0x554883ec"),                                        //NOP : PUSH RBP : SUB RSP, #
            new ByteSequence(Nop, "*", PushRbp, Sub_Rsp_Any),

            new ByteSequence("0xc3 * 0x554883ec"),                                        //RET : PUSH RBP : SUB RSP, #
            new ByteSequence(Ret, "*", PushRbp, Sub_Rsp_Any),

            new ByteSequence("0xcccccc * 0x4883ec"),                                      //CC filler : SUB RSP, #
            new ByteSequence(Int3, Int3, Int3, "*", Sub_Rsp_Any),

            new ByteSequence("0xcccccc * 0x4889 01...100..100100"),                       //CC filler : MOV [RSP+#],R..
            new ByteSequence(Int3, Int3, Int3, "*", Mov_RspDisp_AnyReg),

            new ByteSequence("0xcccccc * 0x488bc4"),                                      //CC filler : MOV RAX,RSP
            new ByteSequence(Int3, Int3, Int3, "*", MovRaxRsp),

            new ByteSequence("0xcc * 0xfff54883ec"),                                      //CC : PUSH RBP : SUB RSP, #
            new ByteSequence(Int3, "*", PushRbpAlt, Sub_Rsp_Any),

            new ByteSequence("0xcc * 0x40 01010... 0x4883ec"),                            //CC : PUSH Rxx : SUB RSP, #
            new ByteSequence(Int3, "*", $"0x{x64Byte.Rex}", PushAny, Sub_Rsp_Any),

            new ByteSequence("0xcc * 0x554883ec"),                                        //CC : PUSH RBP : SUB RSP, #
            new ByteSequence(Int3, "*", PushRbp, Sub_Rsp_Any),

            new ByteSequence("0xcccccc * 0x4055488b 11101..."),                           //CC filler : PUSH RBP : MOV RBP, ...
            new ByteSequence(Int3, Int3, Int3, "*", RexPushRbp, MovRbpAny),

            new ByteSequence("0xcc * 0x4c8b 11...100 0x4883ec"),                          //CC : MOV -,RSP : SUB RSP,#
            new ByteSequence(Int3, "*", $"0x{x64Byte.RexWR}{x64Byte.OpCode.Mov_8B}", x64Byte.ModRM.AnyRsp, Sub_Rsp_Any),

            new ByteSequence("0xcccc * 0x4c8b 11...100 01001.01 0x89"),                   //CC filler : MOV -,RSP : MOV [- + #],
            new ByteSequence(Int3, Int3, "*",  $"0x{x64Byte.RexWR}{x64Byte.OpCode.Mov_8B}", x64Byte.ModRM.AnyRsp, RexWB_AnyR_Mov),
        };

        public static ByteSequence[] GetPatterns()
        {
        }
    }
}
