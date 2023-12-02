using System;
using System.Linq;
using ChaosDbg.Cordb;
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
                Assert.AreEqual(expected[i], actual[i].ToString());
        }
    }
}
