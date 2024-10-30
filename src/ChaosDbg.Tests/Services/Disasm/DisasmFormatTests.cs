using System;
using ChaosDbg.Disasm;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    [TestClass]
    public class DisasmFormatTests : BaseDisasmTest
    {
        [TestMethod]
        public void DisasmFormat_DbgEng_IP_0_x86() =>
            Test<int>(0, "00000000");

        [TestMethod]
        public void DisasmFormat_DbgEng_IP_0_x64() =>
            Test<long>(0, "00000000`00000000");

        [TestMethod]
        public void DisasmFormat_DbgEng_IP_Normal_x86() =>
            Test<int>(0x76ee77c0, "76ee77c0");

        [TestMethod]
        public void DisasmFormat_DbgEng_IP_Normal_x64() =>
            Test<long>(0x7ffbeb40274c, "00007ffb`eb40274c");

        [TestMethod]
        public void DisasmFormat_DbgEng_Operand_Digit()
        {
            //Small numbers should be displayed as digits
            Test("00000001 6a01            push    1", 0x6a, 0x01);
        }

        [TestMethod]
        public void DisasmFormat_DbgEng_Operand_Hex() =>
            Test("00000001 b9ffef0000      mov     ecx,0EFFFh", 0xb9, 0xff, 0xef, 0x00, 0x00);

        [TestMethod]
        public void DisasmFormat_DbgEng_PtrOffset_Digit() =>
            Test("00000001 ff7508          push    dword ptr [ebp+8]", 0xff, 0x75, 0x08);

        [TestMethod]
        public void DisasmFormat_DbgEng_PtrOffset_Hex() =>
            Test("00000001 ff7510          push    dword ptr [ebp+10h]", 0xff, 0x75, 0x10);

        [TestMethod]
        public void DisasmFormat_DbgEng_IPOffset_Hex() =>
            Test(new byte[]{0x83, 0x3d, 0x86, 0xe5, 0xde, 0xff, 0x00}, "00007ffe`a7565f14 833d86e5deff00  cmp     dword ptr [00007ffe`a73544a1],0", 0x00007ffea7565f13, false);

        [TestMethod]
        public void DisasmFormat_ToString_DbgEng()
        {
            Test(
                new byte[] { 0xff, 0x75, 0x08 },
                "00000001 ff7508          push    dword ptr [ebp+8]",
                format: DisasmFormatOptions.DbgEng
            );
        }

        [TestMethod]
        public void DisasmFormat_ToString_NoIPOrBytes()
        {
            Test(
                new byte[] { 0xff, 0x75, 0x08 },
                "push    dword ptr [ebp+8]",
                format: DisasmFormatOptions.Default
            );
        }

        [TestMethod]
        public void DisasmFormat_ToString_IPOnly()
        {
            Test(
                new byte[] { 0xff, 0x75, 0x08 },
                "00000001 push    dword ptr [ebp+8]",
                format: DisasmFormatOptions.Default.WithIP(true)
            );
        }

        [TestMethod]
        public void DisasmFormat_ToString_BytesOnly()
        {
            Test(
                new byte[] { 0xff, 0x75, 0x08 },
                "ff7508          push    dword ptr [ebp+8]",
                format: DisasmFormatOptions.Default.WithBytes(true)
            );
        }

        private void Test(string expected, params byte[] value) =>
            Test(value, expected);

        private void Test<T>(T value, string expected, long ip = 0, bool is32Bit = true, DisasmFormatOptions format = null)
        {
            var formatter = DbgEngFormatter.Default;

            string actual;

            if (typeof(T) == typeof(int))
                actual = formatter.Formatter.FormatInt32((int) (object) value, formatter.ImmediateOptions);
            else if (typeof(T) == typeof(long))
                actual = formatter.Formatter.FormatInt64((long) (object) value, formatter.ImmediateOptions);
            else if (typeof(T) == typeof(byte[]))
            {
                var dis = CreateDisassembler(ip, is32Bit, (byte[])(object)value);
                var instr = dis.Disassemble();
                actual = dis.Format(instr, format);
            }
            else
                throw new NotImplementedException($"Don't know how to format value of type '{typeof(T).Name}'");

            Assert.AreEqual(expected, actual);
        }
    }
}
