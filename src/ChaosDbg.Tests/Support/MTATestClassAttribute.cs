using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    //Sometimes tests are automatically run as MTA, other times not. As such, we implement a custom TestClassAttribute
    //to ensure that we're running inside an MTA if we're not already
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
