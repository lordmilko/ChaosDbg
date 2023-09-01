using System;
using ChaosDbg.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    struct CodeNavigatorVerifier
    {
        private CodeNavigator nav;

        public CodeNavigatorVerifier(CodeNavigator nav)
        {
            this.nav = nav;
        }

        public CodeNavigatorVerifier StepUp(int count, int expected)
        {
            var result = nav.StepUp(count);

            Assert.AreEqual(expected, result);

            return this;
        }

        public CodeNavigatorVerifier StepDown(int count, int expected)
        {
            var result = nav.StepDown(count);

            Assert.AreEqual(expected, result);

            return this;
        }

        public CodeNavigatorVerifier SeekVertical(int newOffset, int expected)
        {
            var result = nav.SeekVertical(newOffset);

            Assert.AreEqual(expected, result);

            return this;
        }

        public CodeNavigatorVerifier GetLines(int startRVA, int endRVA, Action<ITextLine[]> verify)
        {
            var result = nav.GetLines(startRVA, endRVA);

            verify(result);

            return this;
        }
    }
}
