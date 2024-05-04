using ChaosDbg.Evaluator.Masm;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests.Evaluator
{
    [TestClass]
    public class MasmEvaluatorTests
    {
        [TestMethod]
        public void MasmEvaluator_ModuleName() =>
            Test("ntdll", 0x77a60000);

        [TestMethod]
        public void MasmEvaluator_ModuleName_WithDot() =>
            Test("foo.bar", 20);

        [TestMethod]
        public void MasmEvaluator_ModuleAndFunction() =>
            Test("ntdll!LdrInitializeThunk", 0x77abdc60);

        [TestMethod]
        public void MasmEvaluator_ModuleAddress() =>
            TestError("ntdll!1", "Couldn't resolve error at 'ntdll!1'");

        [TestMethod]
        public void MasmEvaluator_Delim_Start() =>
            TestError("`1", "Syntax error at '`1'");

        [TestMethod]
        public void MasmEvaluator_Delim_End() =>
            Test("1`", 1);

        [TestMethod]
        public void MasmEvaluator_Delim_Middle() =>
            Test("1`1", 17);

        [TestMethod]
        public void MasmEvaluator_Hex_LooksLikeIdentifier() =>
            Test("a", 10);

        [TestMethod]
        public void MasmEvaluator_Prefix_Hex() =>
            Test("0xa", 10);

        [TestMethod]
        public void MasmEvaluator_Prefix_HexSpecifier() =>
            Test("0x", 0);

        [TestMethod]
        public void MasmEvaluator_Prefix_Decimal() =>
            Test("0n10", 10);

        [TestMethod]
        public void MasmEvaluator_Prefix_Octal() =>
            Test("0t10", 8);

        [TestMethod]
        public void MasmEvaluator_Prefix_Binary() =>
            Test("0y10", 2);

        [TestMethod]
        public void MasmEvaluator_Suffix_Hex_StartsWithLetter_LooksLikeRegister() =>
            Test("ah", 10);

        [TestMethod]
        public void MasmEvaluator_Suffix_Hex_StartsWithLetter_LooksLikeRegister_WithAt() =>
            Test("@ah", 3);

        [TestMethod]
        public void MasmEvaluator_Suffix_Hex_StartsWithNumber() =>
            Test("1ah", 26);

        [TestMethod]
        public void MasmEvaluator_Math() =>
            Test("1+1", 2);

        //+ is also a valid unary prefix operator, so we want to see how we respond to it
        //vs something that isn't a valid unary prefix operator like *

        [TestMethod]
        public void MasmEvaluator_Math_PartialBinary_Plus_HasLeft() =>
            TestError("1+", "Numeric expression missing from '<EOL>'");

        [TestMethod]
        public void MasmEvaluator_Math_PartialBinary_Plus_HasRight() =>
            Test("+1", 1);

        [TestMethod]
        public void MasmEvaluator_Math_PartialBinary_Star_HasLeft() =>
            TestError("1*", "Numeric expression missing from '<EOL>'");

        [TestMethod]
        public void MasmEvaluator_Math_PartialBinary_Star_HasRight() =>
            TestError("*1", "Numeric expression missing from '*1'");

        [TestMethod]
        public void MasmEvaluator_Register() =>
            Test("esi", 0x032c7c68);

        [TestMethod]
        public void MasmEvaluator_AtRegister() =>
            Test("@esi", 0x032c7c68);

        [TestMethod]
        public void MasmEvaluator_BadRegister() =>
            TestError("@foo", "Bad register error at '@foo'");

        [TestMethod]
        public void MasmEvaluator_Parameters_OrderOfOperations_1() =>
            Test("1 + 2 * 3", 7);

        [TestMethod]
        public void MasmEvaluator_Parameters_OrderOfOperations_2() =>
            Test("(1+1) * 2", 4);

        [TestMethod]
        public void MasmEvaluator_Parameters_OrderOfOperations_3() =>
            Test("1 + (1 * 2)", 3);

        [TestMethod]
        public void MasmEvaluator_Parameters_OrderOfOperations_4() =>
            Test("1 + 2 * 3 + 4", 11);

        [TestMethod]
        public void MasmEvaluator_Parameters_OrderOfOperations_5() =>
            Test("1 + 2 * 3 / 4", 2);

        [TestMethod]
        public void MasmEvaluator_Poi() =>
            Test("poi ntdll!LdrInitializeThunk", 0x8b55ff8b);

        [TestMethod]
        public void MasmEvaluator_Poi_WithParen() =>
            Test("poi(ntdll!LdrInitializeThunk)", 0x8b55ff8b);

        [TestMethod]
        public void MasmEvaluator_Not() =>
            Test("not 0", 1);

        [TestMethod]
        public void MasmEvaluator_Dot() =>
            Test(".", 0x77b18087); //Should get the current IP value

        [TestMethod]
        public void MasmEvaluator_Scope() =>
            Test("foo!bar::baz", 40);

        [TestMethod]
        public void MasmEvaluator_InvalidSymbol() =>
            TestError("foo", "Couldn't resolve error at 'foo'");

        [TestMethod]
        public void MasmEvaluator_InvalidMemory() =>
            TestError("poi 1", "Memory access error at '1'");

        [TestMethod]
        public void MasmEvaluator_UnmatchedParen_HasLeft() =>
            TestError("(1+1", "Syntax error at '<EOL>'");

        //DbgEng does an error '       ^ Extra character error in '? <expr'
        //We won't want to include the fact the user did '?' and also we might not know the width
        //of the current prompt, so we use our own unique error message instead

        [TestMethod]
        public void MasmEvaluator_UnmatchedParen_HasRight() =>
            TestError("1+1)", "Extra characters were encountered: ')'");

        [TestMethod]
        public void MasmEvaluator_Unary_Negative() =>
            Test("-1", -1);

        [TestMethod]
        public void MasmEvaluator_Unary_Plus() =>
            Test("-1", -1);

        [TestMethod]
        public void MasmEvaluator_Unary_Not() =>
            TestError("!1", "Couldn't resolve error at '!1'");

        [TestMethod]
        public void MasmEvaluator_Unary_PlusMinus() =>
            Test("+-1", -1);

        [TestMethod]
        public void MasmEvaluator_Unary_DoubleNegation() =>
            Test("--1", 1);

        [TestMethod]
        public void MasmEvaluator_LogicalOr() =>
            Test("1 or 2", 3);

        [TestMethod]
        public void MasmEvaluator_LogicalAnd() =>
            Test("2 and 6", 2);

        //https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/bitwise-and-shift-operators#unsigned-right-shift-operator-
        //Tested with c# 11 that this does the same thing: Console.WriteLine(((long)-8 >>> 2).ToString("X"));
        [TestMethod]
        public void MasmEvaluator_UnsignedRightShiftExpression() =>
            Test("-8 >>> 2", 0x3FFFFFFFFFFFFFFE);

        private void Test(string expr, long expected)
        {
            var actual = new MasmEvaluator(new TestEvaluatorContext()).Evaluate(expr);

            Assert.AreEqual(expected, actual);
        }

        private void TestError(string expr, string message)
        {
            AssertEx.Throws<InvalidExpressionException>(
                () => new MasmEvaluator(new TestEvaluatorContext()).Evaluate(expr),
                message
            );
        }
    }
}
