using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using ChaosDbg.Evaluator.IL;
using ChaosDbg.IL;
using ChaosLib;
using ChaosLib.Dynamic;
using ChaosLib.Dynamic.Emit;
using ClrDebug;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SymHelp.Metadata;

namespace ChaosDbg.Tests.Evaluator
{
    [TestClass]
    public class ILEvaluatorTests : BaseTest
    {
        #region ILEvaluator

        [ILTestMethod(OpCodeKind.Add)]
        public void ILEvaluator_add_Int32_Int32() => Test((a, b) => a + b, 1, 2);

        [ILTestMethod(OpCodeKind.Add_Ovf)]
        public void ILEvaluator_Add_Ovf() => Test(a => checked(1 + a), 2);

        [ILTestMethod(OpCodeKind.Add_Ovf_Un)]
        public void ILEvaluator_Add_Ovf_Un() => Test((a, b) => checked(a + b), (ulong) 1, (uint) 2);

        [ILTestMethod(OpCodeKind.And)]
        public void ILEvaluator_And_Bool() => Test((a, b) => a && b, true, true);

        [ILTestMethod(OpCodeKind.And)]
        public void ILEvaluator_And_Int32() => Test((a) => false, false);

        [ILTestMethod(OpCodeKind.Arglist)]
        public void ILEvaluator_Arglist() => Test(null);

        [ILTestMethod(OpCodeKind.Beq)]
        public void ILEvaluator_Beq() => Test(null);

        [ILTestMethod(OpCodeKind.Beq_S)]
        public void ILEvaluator_Beq_S() => Test(null);

        [ILTestMethod(OpCodeKind.Bge)]
        public void ILEvaluator_Bge() => Test(null);

        [ILTestMethod(OpCodeKind.Bge_S)]
        public void ILEvaluator_Bge_S() => Test(null);

        [ILTestMethod(OpCodeKind.Bge_Un)]
        public void ILEvaluator_Bge_Un() => Test(null);

        [ILTestMethod(OpCodeKind.Bge_Un_S)]
        public void ILEvaluator_Bge_Un_S() => Test(null);

        [ILTestMethod(OpCodeKind.Bgt)]
        public void ILEvaluator_Bgt() => Test(null);

        [ILTestMethod(OpCodeKind.Bgt_S)]
        public void ILEvaluator_Bgt_S() => Test(null);

        [ILTestMethod(OpCodeKind.Bgt_Un)]
        public void ILEvaluator_Bgt_Un() => Test(null);

        [ILTestMethod(OpCodeKind.Bgt_Un_S)]
        public void ILEvaluator_Bgt_Un_S() => Test(null);

        [ILTestMethod(OpCodeKind.Ble)]
        public void ILEvaluator_Ble() => Test(null);

        [ILTestMethod(OpCodeKind.Ble_S)]
        public void ILEvaluator_Ble_S() => Test(null);

        [ILTestMethod(OpCodeKind.Ble_Un)]
        public void ILEvaluator_Ble_Un() => Test(null);

        [ILTestMethod(OpCodeKind.Ble_Un_S)]
        public void ILEvaluator_Ble_Un_S() => Test(null);

        [ILTestMethod(OpCodeKind.Blt)]
        public void ILEvaluator_Blt() => Test(null);

        [ILTestMethod(OpCodeKind.Blt_S)]
        public void ILEvaluator_Blt_S() => Test(null);

        [ILTestMethod(OpCodeKind.Blt_Un)]
        public void ILEvaluator_Blt_Un() => Test(null);

        [ILTestMethod(OpCodeKind.Blt_Un_S)]
        public void ILEvaluator_Blt_Un_S() => Test(null);

        [ILTestMethod(OpCodeKind.Bne_Un)]
        public void ILEvaluator_Bne_Un() => Test(null);

        [ILTestMethod(OpCodeKind.Bne_Un_S)]
        public void ILEvaluator_Bne_Un_S() => Test(null);

        [ILTestMethod(OpCodeKind.Br)]
        public void ILEvaluator_Br() => Test(null);

        [ILTestMethod(OpCodeKind.Br_S)]
        public void ILEvaluator_Br_S() => Test(null);

        [ILTestMethod(OpCodeKind.Break)]
        public void ILEvaluator_Break() => Test(null);

        [ILTestMethod(OpCodeKind.Brfalse)]
        public void ILEvaluator_Brfalse() => Test(null);

        [ILTestMethod(OpCodeKind.Brfalse_S)]
        public void ILEvaluator_Brfalse_S() => Test(null);

        [ILTestMethod(OpCodeKind.Brtrue)]
        public void ILEvaluator_Brtrue() => Test(null);

        [ILTestMethod(OpCodeKind.Brtrue_S)]
        public void ILEvaluator_Brtrue_S() => Test(null);

        [ILTestMethod(OpCodeKind.Call)]
        public void ILEvaluator_Call() => Test((a, b) => Math.Max(a, b), 100, 200);

        [ILTestMethod(OpCodeKind.Calli)]
        public void ILEvaluator_Calli() => Test(null);

        [ILTestMethod(OpCodeKind.Ceq)]
        public void ILEvaluator_Ceq() => Test((a, b) => a == b, 1, 2);

        [ILTestMethod(OpCodeKind.Cgt)]
        public void ILEvaluator_Cgt() => Test((a, b) => a > b, 2, 1);

        [ILTestMethod(OpCodeKind.Cgt_Un)]
        public void ILEvaluator_Cgt_Un() => Test(null);
        [ILTestMethod(OpCodeKind.Clt)]
        public void ILEvaluator_Clt() => Test((a, b) => a < b, 1, 2);

        [ILTestMethod(OpCodeKind.Clt_Un)]
        public void ILEvaluator_Clt_Un() => Test(null);

        [ILTestMethod(OpCodeKind.Conv_I1)]
        public void ILEvaluator_Conv_I1() => Test(a => (sbyte) a, 1);

        [ILTestMethod(OpCodeKind.Conv_I2)]
        public void ILEvaluator_Conv_I2() => Test(a => (short) a, 1);

        [ILTestMethod(OpCodeKind.Conv_I4)]
        public void ILEvaluator_Conv_I4() => Test(a => (int) a, (ulong) 1);

        [ILTestMethod(OpCodeKind.Conv_I8)]
        public void ILEvaluator_Conv_I8() => Test(a => (long) a, 1);

        [ILTestMethod(OpCodeKind.Conv_R4)]
        public void ILEvaluator_Conv_R4() => Test(a => (float) a, 1);

        [ILTestMethod(OpCodeKind.Conv_R8)]
        public void ILEvaluator_Conv_R8() => Test(a => (double) a, 1);

        [ILTestMethod(OpCodeKind.Conv_U1)]
        public void ILEvaluator_Conv_U1() => Test(a => (byte) a, 1);

        [ILTestMethod(OpCodeKind.Conv_U2)]
        public void ILEvaluator_Conv_U2() => Test(a => (ushort) a, 1);

        [ILTestMethod(OpCodeKind.Conv_U4)]
        public void ILEvaluator_Conv_U4() => Test(a => (uint) a, (ulong) 1);

        [ILTestMethod(OpCodeKind.Conv_U8)]
        public void ILEvaluator_Conv_U8() => Test(a => (ulong) a, (uint) 1);

        [ILTestMethod(OpCodeKind.Conv_I)]
        public void ILEvaluator_Conv_I() => Test(a => (IntPtr) a, 1);

        [ILTestMethod(OpCodeKind.Conv_U)]
        public void ILEvaluator_Conv_U() => Test(a => (UIntPtr) a, 1);

        [ILTestMethod(OpCodeKind.Conv_R_Un)]
        public void ILEvaluator_Conv_R_Un() => Test(null);

        [ILTestMethod(OpCodeKind.Conv_Ovf_I1)]
        public void ILEvaluator_Conv_Ovf_I1() => Test(a => checked((sbyte) a), 1);

        [ILTestMethod(OpCodeKind.Conv_Ovf_I2)]
        public void ILEvaluator_Conv_Ovf_I2() => Test(a => checked((short) a), 1);

        [ILTestMethod(OpCodeKind.Conv_Ovf_I4)]
        public void ILEvaluator_Conv_Ovf_I4() => Test(a => checked((int) a), (long) 1);

        [ILTestMethod(OpCodeKind.Conv_Ovf_I8)]
        public void ILEvaluator_Conv_Ovf_I8() => Test(a => checked((long) a), 1);

        [ILTestMethod(OpCodeKind.Conv_Ovf_U1)]
        public void ILEvaluator_Conv_Ovf_U1() => Test(a => checked((byte) a), 1);

        [ILTestMethod(OpCodeKind.Conv_Ovf_U2)]
        public void ILEvaluator_Conv_Ovf_U2() => Test(a => checked((ushort) a), 1);

        [ILTestMethod(OpCodeKind.Conv_Ovf_U4)]
        public void ILEvaluator_Conv_Ovf_U4() => Test(a => checked((uint) a), 1);

        [ILTestMethod(OpCodeKind.Conv_Ovf_U8)]
        public void ILEvaluator_Conv_Ovf_U8() => Test(a => checked((ulong) a), 1);

        [ILTestMethod(OpCodeKind.Conv_Ovf_I)]
        public void ILEvaluator_Conv_Ovf_I() => Test(null);

        [ILTestMethod(OpCodeKind.Conv_Ovf_U)]
        public void ILEvaluator_Conv_Ovf_U() => Test(null);

        [ILTestMethod(OpCodeKind.Conv_Ovf_I1_Un)]
        public void ILEvaluator_Conv_Ovf_I1_Un() => Test(a => checked((sbyte) a), (ulong) 1);

        [ILTestMethod(OpCodeKind.Conv_Ovf_I2_Un)]
        public void ILEvaluator_Conv_Ovf_I2_Un() => Test(a => checked((short) a), (ulong) 1);

        [ILTestMethod(OpCodeKind.Conv_Ovf_I4_Un)]
        public void ILEvaluator_Conv_Ovf_I4_Un() => Test(a => checked((int) a), (ulong) 1);

        [ILTestMethod(OpCodeKind.Conv_Ovf_I8_Un)]
        public void ILEvaluator_Conv_Ovf_I8_Un() => Test(a => checked((long) a), (ulong) 1);

        [ILTestMethod(OpCodeKind.Conv_Ovf_U1_Un)]
        public void ILEvaluator_Conv_Ovf_U1_Un() => Test(a => checked((byte) a), (ulong) 1);

        [ILTestMethod(OpCodeKind.Conv_Ovf_U2_Un)]
        public void ILEvaluator_Conv_Ovf_U2_Un() => Test(a => checked((ushort) a), (ulong) 1);

        [ILTestMethod(OpCodeKind.Conv_Ovf_U4_Un)]
        public void ILEvaluator_Conv_Ovf_U4_Un() => Test(a => checked((uint) a), (ulong) 1);

        [ILTestMethod(OpCodeKind.Conv_Ovf_U8_Un)]
        public void ILEvaluator_Conv_Ovf_U8_Un() => Test(a => checked((ulong) a), (uint) 1);

        [ILTestMethod(OpCodeKind.Conv_Ovf_I_Un)]
        public void ILEvaluator_Conv_Ovf_I_Un() => Test(null);

        [ILTestMethod(OpCodeKind.Conv_Ovf_U_Un)]
        public void ILEvaluator_Conv_Ovf_U_Un() => Test(null);

        [ILTestMethod(OpCodeKind.Cpblk)]
        public void ILEvaluator_Cpblk() => Test(null);

        [ILTestMethod(OpCodeKind.Div)]
        public void ILEvaluator_div_Int32_Int32() => Test((a, b) => a / b, 5, 3);

        [ILTestMethod(OpCodeKind.Div_Un)]
        public void ILEvaluator_Div_Un() => Test(null);

        [ILTestMethod(OpCodeKind.Dup)]
        public void ILEvaluator_Dup() => Test(null);

        [ILTestMethod(OpCodeKind.Endfilter)]
        public void ILEvaluator_Endfilter() => Test(null);

        [ILTestMethod(OpCodeKind.Endfinally)]
        public void ILEvaluator_Endfinally() => Test(null);

        [ILTestMethod(OpCodeKind.Initblk)]
        public void ILEvaluator_Initblk() => Test(null);

        [ILTestMethod(OpCodeKind.Jmp)]
        public void ILEvaluator_Jmp() => Test(null);
        [ILTestMethod(OpCodeKind.Ldc_I8)]
        public void ILEvaluator_Ldc_I8() => Test((Func<long>) (() => long.MaxValue));

        [ILTestMethod(OpCodeKind.Ldc_R4)]
        public void ILEvaluator_Ldc_R4() => Test((Func<float>) (() => 1.5f));

        [ILTestMethod(OpCodeKind.Ldc_R8)]
        public void ILEvaluator_Ldc_R8() => Test((Func<double>) (() => 1.5));

        [ILTestMethod(OpCodeKind.Ldc_I4_M1)]
        public void ILEvaluator_Ldc_I4_M1() => Test(() => -1);

        [ILTestMethod(OpCodeKind.Ldc_I4_S)]
        public void ILEvaluator_Ldc_I4_S() => Test(() => 100);

        [ILTestMethod(OpCodeKind.Ldftn)]
        public void ILEvaluator_Ldftn() => Test(null);

        [ILTestMethod(OpCodeKind.Ldind_I1)]
        public void ILEvaluator_Ldind_I1() => Test(null);

        [ILTestMethod(OpCodeKind.Ldind_I2)]
        public void ILEvaluator_Ldind_I2() => Test(null);

        [ILTestMethod(OpCodeKind.Ldind_I4)]
        public void ILEvaluator_Ldind_I4() => Test(null);

        [ILTestMethod(OpCodeKind.Ldind_I8)]
        public void ILEvaluator_Ldind_I8() => Test(null);

        [ILTestMethod(OpCodeKind.Ldind_U1)]
        public void ILEvaluator_Ldind_U1() => Test(null);

        [ILTestMethod(OpCodeKind.Ldind_U2)]
        public void ILEvaluator_Ldind_U2() => Test(null);

        [ILTestMethod(OpCodeKind.Ldind_U4)]
        public void ILEvaluator_Ldind_U4() => Test(null);

        [ILTestMethod(OpCodeKind.Ldind_R4)]
        public void ILEvaluator_Ldind_R4() => Test(null);

        [ILTestMethod(OpCodeKind.Ldind_R8)]
        public void ILEvaluator_Ldind_R8() => Test(null);

        [ILTestMethod(OpCodeKind.Ldind_I)]
        public void ILEvaluator_Ldind_I() => Test(null);

        [ILTestMethod(OpCodeKind.Ldind_Ref)]
        public void ILEvaluator_Ldind_Ref() => Test(null);

        [ILTestMethod(OpCodeKind.Ldloc)]
        public void ILEvaluator_Ldloc() => Test(null);

        [ILTestMethod(OpCodeKind.Ldloc_S)]
        public void ILEvaluator_Ldloc_S() => Test(null);

        [ILTestMethod(OpCodeKind.Ldloc_0)]
        public void ILEvaluator_Ldloc_0() => Test(null);

        [ILTestMethod(OpCodeKind.Ldloc_1)]
        public void ILEvaluator_Ldloc_1() => Test(null);

        [ILTestMethod(OpCodeKind.Ldloc_2)]
        public void ILEvaluator_Ldloc_2() => Test(null);

        [ILTestMethod(OpCodeKind.Ldloc_3)]
        public void ILEvaluator_Ldloc_3() => Test(null);

        [ILTestMethod(OpCodeKind.Ldloca)]
        public void ILEvaluator_Ldloca() => Test(null);

        [ILTestMethod(OpCodeKind.Ldloca_S)]
        public void ILEvaluator_Ldloca_S() => Test(null);

        [ILTestMethod(OpCodeKind.Ldnull)]
        public void ILEvaluator_Ldnull() => Test<object>(() => null);

        [ILTestMethod(OpCodeKind.Leave)]
        public void ILEvaluator_Leave() => Test(null);

        [ILTestMethod(OpCodeKind.Leave_S)]
        public void ILEvaluator_Leave_S() => Test(null);

        [ILTestMethod(OpCodeKind.Localloc)]
        public void ILEvaluator_Localloc() => Test(null);

        [ILTestMethod(OpCodeKind.Mul)]
        public void ILEvaluator_mul_Int32_Int32() => Test((a, b) => a * b, 2, 3);

        [ILTestMethod(OpCodeKind.Mul_Ovf)]
        public void ILEvaluator_Mul_Ovf() => Test(null);

        [ILTestMethod(OpCodeKind.Mul_Ovf_Un)]
        public void ILEvaluator_Mul_Ovf_Un() => Test(null);

        [ILTestMethod(OpCodeKind.Neg)]
        public void ILEvaluator_Neg() => Test(null);

        [ILTestMethod(OpCodeKind.Nop)]
        public void ILEvaluator_Nop() => Test(null);

        [ILTestMethod(OpCodeKind.Not)]
        public void ILEvaluator_Not() => Test(null);

        [ILTestMethod(OpCodeKind.Or)]
        public void ILEvaluator_Or() => Test(null);

        [ILTestMethod(OpCodeKind.Pop)]
        public void ILEvaluator_Pop() => Test(null);

        [ILTestMethod(OpCodeKind.Rem)]
        public void ILEvaluator_Rem() => Test(null);

        [ILTestMethod(OpCodeKind.Rem_Un)]
        public void ILEvaluator_Rem_Un() => Test(null);

        [ILTestMethod(OpCodeKind.Ret)]
        public void ILEvaluator_Ret() => Test((a, b) => a + b, 1, 2);

        [ILTestMethod(OpCodeKind.Shl)]
        public void ILEvaluator_Shl() => Test((a, b) => a << b, 1, 3);

        [ILTestMethod(OpCodeKind.Shr)]
        public void ILEvaluator_Shr() => Test((a, b) => a >> b, 128, 3);

        [ILTestMethod(OpCodeKind.Shr_Un)]
        public void ILEvaluator_Shr_Un() => Test(null);

        [ILTestMethod(OpCodeKind.Starg)]
        public void ILEvaluator_Starg() => Test(null);

        [ILTestMethod(OpCodeKind.Starg_S)]
        public void ILEvaluator_Starg_S() => Test(null);

        [ILTestMethod(OpCodeKind.Stind_I1)]
        public unsafe void ILEvaluator_Stind_I1() => Test(() =>
        {
            byte val = 3;
            var ptr = &val;
            *ptr = 2;
            return val;
        });

        [ILTestMethod(OpCodeKind.Stind_I2)]
        public unsafe void ILEvaluator_Stind_I2() => Test(() =>
        {
            short val = 3;
            var ptr = &val;
            *ptr = 2;
            return val;
        });

        [ILTestMethod(OpCodeKind.Stind_I4)]
        public unsafe void ILEvaluator_Stind_I4() => Test(() =>
        {
            int val = 3;
            var ptr = &val;
            *ptr = 2;
            return val;
        });

        [ILTestMethod(OpCodeKind.Stind_I8)]
        public unsafe void ILEvaluator_Stind_I8() => Test((Func<long>) (() =>
        {
            long val = 3;
            var ptr = &val;
            *ptr = 2;
            return val;
        }));

        [ILTestMethod(OpCodeKind.Stind_R4)]
        public unsafe void ILEvaluator_Stind_R4() => Test((Func<float>) (() =>
        {
            float val = 3.2f;
            var ptr = &val;
            *ptr = 2.4f;
            return val;
        }));

        [ILTestMethod(OpCodeKind.Stind_R8)]
        public unsafe void ILEvaluator_Stind_R8() => Test((Func<double>) (() =>
        {
            double val = 3.2;
            var ptr = &val;
            *ptr = 2.4;
            return val;
        }));

        [ILTestMethod(OpCodeKind.Stind_I)]
        public unsafe void ILEvaluator_Stind_I() => Test((Func<IntPtr>) (() =>
        {
            IntPtr val = new IntPtr(1);
            var ptr = &val;
            *ptr = new IntPtr(2);
            return val;
        }));

        [ILTestMethod(OpCodeKind.Stind_Ref)]
        public void ILEvaluator_Stind_Ref() => Test(null);
        [ILTestMethod(OpCodeKind.Sub)]
        public void ILEvaluator_sub_Int32_Int32() => Test((a, b) => a - b, 2, 1);

        [ILTestMethod(OpCodeKind.Sub_Ovf)]
        public void ILEvaluator_Sub_Ovf() => Test(null);

        [ILTestMethod(OpCodeKind.Sub_Ovf_Un)]
        public void ILEvaluator_Sub_Ovf_Un() => Test(null);

        [ILTestMethod(OpCodeKind.Switch)]
        public void ILEvaluator_Switch() => Test(null);

        [ILTestMethod(OpCodeKind.Xor)]
        public void ILEvaluator_Xor() => Test(null);

        [ILTestMethod(OpCodeKind.Box)]
        public void ILEvaluator_Box() => Test(a => (object) a, 1);

        [ILTestMethod(OpCodeKind.Callvirt)]
        public void ILEvaluator_Callvirt() => Test(null);

        [ILTestMethod(OpCodeKind.Castclass)]
        public void ILEvaluator_Castclass() => Test<object, string>(a => (string) a, "hello");

        [ILTestMethod(OpCodeKind.Cpobj)]
        public void ILEvaluator_Cpobj() => Test(null);

        [ILTestMethod(OpCodeKind.Initobj)]
        public void ILEvaluator_Initobj() => Test(null);

        [ILTestMethod(OpCodeKind.Isinst)]
        public void ILEvaluator_Isinst() => Test(null);

        [ILTestMethod(OpCodeKind.Ldelem)]
        public void ILEvaluator_Ldelem() => Test(null);

        [ILTestMethod(OpCodeKind.Ldelem_I1)]
        public void ILEvaluator_Ldelem_I1() => Test(null);

        [ILTestMethod(OpCodeKind.Ldelem_I2)]
        public void ILEvaluator_Ldelem_I2() => Test(null);

        [ILTestMethod(OpCodeKind.Ldelem_I4)]
        public void ILEvaluator_Ldelem_I4() => Test(null);

        [ILTestMethod(OpCodeKind.Ldelem_I8)]
        public void ILEvaluator_Ldelem_I8() => Test(null);

        [ILTestMethod(OpCodeKind.Ldelem_U1)]
        public void ILEvaluator_Ldelem_U1() => Test(null);

        [ILTestMethod(OpCodeKind.Ldelem_U2)]
        public void ILEvaluator_Ldelem_U2() => Test(null);

        [ILTestMethod(OpCodeKind.Ldelem_U4)]
        public void ILEvaluator_Ldelem_U4() => Test(null);

        [ILTestMethod(OpCodeKind.Ldelem_R4)]
        public void ILEvaluator_Ldelem_R4() => Test(null);

        [ILTestMethod(OpCodeKind.Ldelem_R8)]
        public void ILEvaluator_Ldelem_R8() => Test(null);

        [ILTestMethod(OpCodeKind.Ldelem_I)]
        public void ILEvaluator_Ldelem_I() => Test(null);

        [ILTestMethod(OpCodeKind.Ldelem_Ref)]
        public void ILEvaluator_Ldelem_Ref() => Test(null);

        [ILTestMethod(OpCodeKind.Ldelema)]
        public void ILEvaluator_Ldelema() => Test(null);

        private int instanceField1 = 20;
        private int staticField1 = 20;

        [ILTestMethod(OpCodeKind.Ldfld)]
        public void ILEvaluator_Ldfld() => Test(() => instanceField1);

        [ILTestMethod(OpCodeKind.Ldflda)]
        public void ILEvaluator_Ldflda() => Test(null);

        [ILTestMethod(OpCodeKind.Ldlen)]
        public void ILEvaluator_Ldlen() => Test(a => a.Length, new object[1]);

        [ILTestMethod(OpCodeKind.Ldobj)]
        public void ILEvaluator_Ldobj() => Test(null);

        [ILTestMethod(OpCodeKind.Ldsfld)]
        public void ILEvaluator_Ldsfld() => Test(() => staticField1);

        [ILTestMethod(OpCodeKind.Ldsflda)]
        public void ILEvaluator_Ldsflda() => Test(null);

        [ILTestMethod(OpCodeKind.Ldstr)]
        public void ILEvaluator_Ldstr() => Test(() => "hello");

        [ILTestMethod(OpCodeKind.Ldtoken)]
        public void ILEvaluator_Ldtoken() => Test(() => typeof(string));

        [ILTestMethod(OpCodeKind.Ldvirtftn)]
        public void ILEvaluator_Ldvirtftn() => Test(null);

        [ILTestMethod(OpCodeKind.Mkrefany)]
        public void ILEvaluator_Mkrefany() => Test(null);

        [ILTestMethod(OpCodeKind.Newarr)]
        public void ILEvaluator_Newarr() => Test(() => new int[1]);

        [ILTestMethod(OpCodeKind.Newobj)]
        public void ILEvaluator_Newobj() => Test(() => new object());

        [ILTestMethod(OpCodeKind.Refanytype)]
        public void ILEvaluator_Refanytype() => Test(null);

        [ILTestMethod(OpCodeKind.Refanyval)]
        public void ILEvaluator_Refanyval() => Test(null);

        [ILTestMethod(OpCodeKind.Rethrow)]
        public void ILEvaluator_Rethrow() => Test(null);

        [ILTestMethod(OpCodeKind.Sizeof)]
        public void ILEvaluator_Sizeof() => Test(null);

        [ILTestMethod(OpCodeKind.Stelem)]
        public void ILEvaluator_Stelem() => Test(null);

        [ILTestMethod(OpCodeKind.Stelem_I1)]
        public void ILEvaluator_Stelem_I1() => Test(null);

        [ILTestMethod(OpCodeKind.Stelem_I2)]
        public void ILEvaluator_Stelem_I2() => Test(null);

        [ILTestMethod(OpCodeKind.Stelem_I4)]
        public void ILEvaluator_Stelem_I4() => Test(null);

        [ILTestMethod(OpCodeKind.Stelem_I8)]
        public void ILEvaluator_Stelem_I8() => Test(null);

        [ILTestMethod(OpCodeKind.Stelem_R4)]
        public void ILEvaluator_Stelem_R4() => Test(null);

        [ILTestMethod(OpCodeKind.Stelem_R8)]
        public void ILEvaluator_Stelem_R8() => Test(null);

        [ILTestMethod(OpCodeKind.Stelem_I)]
        public void ILEvaluator_Stelem_I() => Test(null);

        [ILTestMethod(OpCodeKind.Stelem_Ref)]
        public void ILEvaluator_Stelem_Ref() => Test(null);

        private int instanceField2;

        [ILTestMethod(OpCodeKind.Stfld)]
        public void ILEvaluator_Stfld() => Test(() =>
        {
            instanceField2 = 30;
            return instanceField2;
        });

        [ILTestMethod(OpCodeKind.Stobj)]
        public void ILEvaluator_Stobj() => Test(null);

        [ILTestMethod(OpCodeKind.Stsfld)]
        public void ILEvaluator_Stsfld() => Test(null);

        [ILTestMethod(OpCodeKind.Throw)]
        public void ILEvaluator_Throw() => Test(null);

        [ILTestMethod(OpCodeKind.Unbox)]
        public void ILEvaluator_Unbox() => Test(null);

        [ILTestMethod(OpCodeKind.Unbox_Any)]
        public void ILEvaluator_Unbox_Any() => Test(a => (int) a, (object) 1);

        private void Test<TRet>(Func<TRet> func) =>
            Test((Delegate) func);

        private void Test<T1, TRet>(Func<T1, TRet> func, T1 a) =>
            Test((Delegate) func, a);

        private void Test<T1, T2, TRet>(Func<T1, T2, TRet> func, T1 a, T2 b) =>
            Test((Delegate) func, a, b);
        private ILInstruction[] Disassemble(MethodInfo method)
        {
            var store = new ReflectionModuleMetadataStore();
            var metadataModule = store.GetOrAddModule(method.Module);

            var methodBody = method.GetMethodBody();

            var bytes = methodBody.GetILAsByteArray();

            var dis = ILDisassembler.Create(bytes, metadataModule);

            var instrs = dis.EnumerateInstructions().ToArray();

            return instrs;
        }
    }
}
