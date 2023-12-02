using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    class MTATestClassAttribute : TestClassAttribute
    {
        public override TestMethodAttribute GetTestMethodAttribute(TestMethodAttribute testMethodAttribute)
        {
            if (testMethodAttribute is MTATestMethodAttribute)
                return testMethodAttribute;

            return new MTATestMethodAttribute(testMethodAttribute);
        }
    }
}
