using System;
using System.Collections.Generic;
using System.Linq;
using static ChaosDbg.Analysis.InstrBytes;

namespace ChaosDbg.Analysis
{
    class x64ByteSequence
    {
        //XFG Compatible
        public static readonly ByteSequence Int3Int3Int3_MARK_SubRspAny          = new ByteSequence(Int3, Int3, Int3, "*", Sub_Rsp_Any);
        public static readonly ByteSequence Int3Int3_MARK_RexPushAny_SubRspAny = new ByteSequence(Int3, Int3, "*", RexPushAny, Sub_Rsp_Any);
        public static readonly ByteSequence Int3Int3Int3_MARK_RexPushAny = new ByteSequence(Int3, Int3, Int3, "*", RexPushAny);

        public static readonly ByteSequence Int3Int3_MARK_MovRspDispAnyReg       = new ByteSequence(Int3, Int3, "*", Mov_RspDisp_AnyReg);
        public static readonly ByteSequence Int3Int3_MARK_MovAnyRsp              = new ByteSequence(Int3, Int3, "*", MovAnyRsp);
        public static readonly ByteSequence RetInt3_MARK_MovAnyRsp               = new ByteSequence(Ret, Int3, "*", MovAnyRsp);
        public static readonly ByteSequence Extra1                               = new ByteSequence(Ret, "*", "0x4C8BD1");
        public static readonly ByteSequence Extra2                               = new ByteSequence(Int3, Int3, Int3, "*", "0x4C8BD1");
        public static readonly ByteSequence Int3Int3Int3_MARK_RexPushAny_PushAny = new ByteSequence(Int3, Int3, Int3, "*", RexPushAny, PushAny);

        //Non XFG
        public static readonly ByteSequence Int3_MARK_RexPushAny_SubRspAny = new ByteSequence(Int3, "*", RexPushAny, Sub_Rsp_Any);
        public static readonly ByteSequence RexInt3_MARK_RexPushAny_PushRbp = new ByteSequence(Ret, Int3, "*", RexPushAny, PushRbp);
        public static readonly ByteSequence Extra3 = new ByteSequence("0xe9........", "*", RexPushAny, PushRbp);

        public static ByteSequence[] XfgCompatiblePatterns =
        {
            //See https://github.com/lordmilko/ChaosDbg/issues/18
        };

        public static ByteSequence[] NonXfgPatterns =
        {
            /*new ByteSequence(Nop,  Nop,  Nop,  "*", Sub_Rsp_Any),
            

            new ByteSequence(Nop,  Nop,  Nop,  "*", Mov_RspDisp_AnyReg),
            

            new ByteSequence(            Nop,  "*", Mov_RspDisp_AnyReg_AnyDisp, RexMov),

            new ByteSequence(            Nop,  "*", Mov_RspDisp_AnyReg_ArgDisp, PushAny, PushAny),
            new ByteSequence(            Ret,  "*", Mov_RspDisp_AnyReg_ArgDisp, PushAny, PushAny),

            new ByteSequence(            Ret,  "*", PushRbp, LeaRbpRsp),
            new ByteSequence(            Nop,  "*", PushRbp, LeaRbpRsp),

            new ByteSequence(            Nop,  "*", PushAny, PushRbp, LeaRbpRsp),
            new ByteSequence(            Ret,  "*", PushAny, PushRbp, LeaRbpRsp),

            new ByteSequence(Nop,  Nop,  Nop,  "*", MovRaxRsp),
            new ByteSequence(Int3, Int3, Int3, "*", MovRaxRsp),

            new ByteSequence(            Nop,  "*", PushRbpAlt, Sub_Rsp_Any),
            new ByteSequence(            Int3, "*", PushRbpAlt, Sub_Rsp_Any),

            new ByteSequence(            Nop,  "*", PushRbp, Sub_Rsp_Any),
            new ByteSequence(            Ret,  "*", PushRbp, Sub_Rsp_Any),*/

            //4055
            Int3_MARK_RexPushAny_SubRspAny,

            /*new ByteSequence(            Int3, "*", PushRbp, Sub_Rsp_Any),
            new ByteSequence(Int3, Int3, Int3, "*", RexPushRbp, MovRbpAny),
            new ByteSequence(            Int3, "*", MovAnyRsp, Sub_Rsp_Any),*/
            

            //Extra
            
            //4053
            RexInt3_MARK_RexPushAny_PushRbp,

            //4053
            Extra3, //jmp ? / push ? / push rbp
            //new ByteSequence("0xe9........", "*", RexPushRbp, Sub_Rsp_Any) //jmp ? / push rbp / sub rsp, ?
        };

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

        [Obsolete]
        internal static void TestGhidra()
        {
            for (var i = 0; i < x64Ghidra.Length; i++)
            {
                var oldV = x64Ghidra[i];
                var newV = x64Ghidra[i + 1];

                if (oldV.ToString() != newV.ToString())
                    throw new NotImplementedException($"Expected our Ghidra's pattern definition '{oldV}' to match our typed interpretation '{newV}'. There's an issue with our typed interpreter");

                i++;
            }
        }

        public static ByteSequence[] GetPatterns()
        {
            var patterns = new List<ByteSequence>();

            patterns.AddRange(XfgCompatiblePatterns);
            patterns.AddRange(NonXfgPatterns);
            patterns.AddRange(XfgCompatiblePatterns.Select(v => v.WithXfg()));

            return patterns.ToArray();
        }
    }
}
