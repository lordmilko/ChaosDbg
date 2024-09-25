using System;
using System.Linq;
using System.Text.RegularExpressions;
using ChaosDbg.Cordb;
using ChaosDbg.DbgEng;
using ChaosDbg.Disasm;
using Iced.Intel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    class CordbThreadVerifier
    {
        private CordbThread thread;

        public CordbThreadVerifier(CordbThread thread)
        {
            this.thread = thread;
        }

        public void StackTrace(params string[] expected)
        {
            var actual = thread.StackTrace;

            if (expected.Length != actual.Length)
            {
                var allExpected = string.Join(Environment.NewLine, expected);
                var allActual = string.Join(Environment.NewLine, actual.Select(v => v.ToString()));

                Assert.AreEqual(allExpected, allActual, $"Number of frames was incorrect (expected: {expected.Length}, actual: {actual.Length})");
            }

            for (var i = 0; i < expected.Length; i++)
            {
                var expectedItem = expected[i];
                var actualItem = actual[i].ToString();

                Assert.AreEqual(expectedItem, actualItem);
            }
        }

        public void IL(string methodName, params string[] expected)
        {
            var frame = thread.StackTrace.OfType<CordbILFrame>().First();

            var function = frame.Function;

            Assert.AreEqual(methodName, function.ToString());

            var il = function.IL;

            if (expected.Length != il.Length)
            {
                var allExpected = string.Join(Environment.NewLine, expected);
                var allActual = string.Join(Environment.NewLine, il.Select(v => v.ToString()));

                Assert.AreEqual(allExpected, allActual, $"Number of instructions was incorrect (expected: {expected.Length}, actual: {il.Length})");
            }

            for (var i = 0; i < expected.Length; i++)
            {
                var actualItem = il[i].ToString();

                Assert.AreEqual(expected[i], actualItem);
            }
        }

        public void Disasm(DbgEngEngine dbgEngEngine, string methodName, string[] x86Expected, string[] x64Expected)
        {
            var expected = thread.Process.Is32Bit ? x86Expected : x64Expected;

            var frame = thread.StackTrace.OfType<CordbILFrame>().First();
            var function = frame.Function;

            Assert.AreEqual(methodName, function.ToString());

            //First, compare our disassembly against SOS

            var disasm = function.Disassembly;
            var fnAddr = function.CorDebugFunction.NativeCode.CodeChunks.Single().startAddr;
            var sosDisasm = dbgEngEngine.ExecuteBufferedCommand($"!u -n {fnAddr}");

            CompareAgainstSOS(sosDisasm, disasm);

            var disasmStrs = CleanDisasm(disasm);

            //Now, compare our expected assembly vs what we actually got

            if (expected.Length != disasmStrs.Length)
            {
                var allExpected = string.Join(Environment.NewLine, expected);
                var allActual = string.Join(Environment.NewLine, disasmStrs.Select(v => v.ToString()));

                Assert.AreEqual(allExpected, allActual, $"Number of instructions was incorrect (expected: {expected.Length}, actual: {disasmStrs.Length})");
            }

            for (var i = 0; i < expected.Length; i++)
            {
                var expectedItem = expected[i];
                var actualItem = disasmStrs[i];

                //If this is a call, the bytes describing the address of the target (e.g. CORINFO_HELP_DBG_IS_JUST_MY_CODE)
                //can be slightly different, so ignore the bytes

                switch (disasm[i].Instruction.Mnemonic)
                {
                    case Mnemonic.Call:
                        expectedItem = expectedItem.Substring(expectedItem.IndexOf(' ')).TrimStart();
                        actualItem = actualItem.Substring(actualItem.IndexOf(' ')).TrimStart();
                        break;

                    default:
                        //If there's a specific memory address referenced, the bytes might be different also
                        if (expectedItem.Contains("[<memory>]"))
                            goto case Mnemonic.Call;

                        break;
                }

                Assert.AreEqual(expectedItem, actualItem);
            }
        }

        private void CompareAgainstSOS(string[] sos, INativeInstruction[] disasm)
        {
            sos = sos.SkipWhile(v => !v.StartsWith("Begin")).Skip(1).Where(v =>
            {
                if (v.StartsWith("*** WARNING"))
                    return false;

                return true;
            }).Select(v =>
            {
                if (v.StartsWith(">>> "))
                    return v.Substring(4);

                return v;
            }).ToArray();

            Assert.AreEqual(disasm.Length, sos.Length, "ChaosDbg Disasm and SOS did not have the same number of lines");

            for (var i = 0; i < sos.Length; i++)
            {
                var expectedSOS = sos[i];
                var expectedDis = disasm[i].ToString();

                //We are actually better than SOS at deriving symbols from memory addresses.
                //Also, SOS uses a wildly different display strategy to us (it displays the symbol
                //after the native address, rather than instead of it). As such, we say that if we both have
                //some kind of symbol, we'll consider ourselves a match

                if (IsAcceptableMatch(expectedSOS, expectedDis))
                    continue;

                //If any module names are referenced in symbols, change _ back to .
                //If a symbol contained a _, we should have deemed it an acceptable match above
                expectedSOS = expectedSOS.Replace("_", ".");

                Assert.AreEqual(expectedSOS, expectedDis, "SOS and ChaosDbg Disasm were not the same");
            }
        }

        private bool IsAcceptableMatch(string sos, string dis)
        {
            //First, try and find a symbol in the SOS output

            var jitMatch = Regex.Match(sos, ".+\\(JitHelp: (.+?)\\)");

            if (jitMatch.Success)
            {
                var jitHelper = jitMatch.Groups[1].Value;

                if (dis.Contains(jitHelper))
                    return true;
            }

            //There is a real symbol clr!JIT_DbgIsJustMyCode
            if (dis.Contains("CORINFO_HELP_DBG_IS_JUST_MY_CODE") && sos.Contains("JIT_DbgIsJustMyCode"))
                return true;

            var symbolMatch = Regex.Match(sos, ".+ \\((.+?), mdToken:.+?\\)");

            if (symbolMatch.Success)
            {
                var sym = symbolMatch.Groups[1].Value;

                if (dis.Contains(sym))
                    return true;

                //If we don't contain the symbol (e.g. Thread.SleepInternal) but do contain a CLR internal symbol (clr!ThreadNative::Sleep) that's acceptable
                if (dis.Contains("clr!"))
                    return true;
            }

            //If we have a symbol and SOS doesn't, we did better

            if (dis.Contains("System"))
                return true;

            //If neither had symbols, but we resolved the indirect pointer to something, we did better
            if (dis.StartsWith(sos.Replace("_", ".") + " ds:"))
                return true;

            return false;
        }

        private string[] CleanDisasm(INativeInstruction[] disasm)
        {
            //We can't unit test against a bunch of memory addresses that will constantly change

            var strs = disasm.Select(v => v.ToString()).ToArray();

            //Strip off the leading memory address
            for (var i = 0; i < strs.Length; i++)
                strs[i] = strs[i].Substring(strs[i].IndexOf(' ') + 1);

            //Remove any memory addresses in brackets
            for (var i = 0; i < strs.Length; i++)
                strs[i] = Regex.Replace(strs[i], "(.+)( \\(.+?\\))", "$1");

            //Clear out any memory addresses in brackets
            for (var i = 0; i < strs.Length; i++)
            {
                if (IntPtr.Size == 4)
                    strs[i] = Regex.Replace(strs[i], "\\[.{8}\\]", "[<memory>]");
                else
                    strs[i] = Regex.Replace(strs[i], "\\[.{8}`.{8}\\]", "[<memory>]");
            }

            return strs;
        }
    }
}
