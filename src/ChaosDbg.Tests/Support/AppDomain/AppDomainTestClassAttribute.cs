using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    class AppDomainTestClassAttribute : TestClassAttribute
    {
        public override TestMethodAttribute GetTestMethodAttribute(TestMethodAttribute testMethodAttribute)
        {
            if (testMethodAttribute is AppDomainTestMethodAttribute)
                return testMethodAttribute;

            return new AppDomainTestMethodAttribute(testMethodAttribute);
        }
    }
}
