using ChaosDbg.Disasm;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    static class VerifyExtensions
    {
        public static void Verify(this INativeDisassembler engine, string defaultFormat, string customFormat)
        {
            var result = engine.Disassemble(0, 1);

            Assert.AreEqual(1, result.Length, "Expected number of disassembled instructions was not correct");

            Assert.AreEqual(defaultFormat, result[0].Instruction.ToString(), "Default instruction format was not correct");
            Assert.AreEqual(customFormat, engine.Format(result[0]), "Custom instruction format was not correct");
        }
    }
}
