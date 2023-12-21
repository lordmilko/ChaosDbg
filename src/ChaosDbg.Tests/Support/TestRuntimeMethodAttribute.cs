using System.Collections.Generic;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    class TestRuntimeMethodAttribute : TestMethodAttribute, ITestDataSource
    {
        public IEnumerable<object[]> GetData(MethodInfo methodInfo)
        {
            yield return new object[] {true};
            yield return new object[] {false};
        }

        public string GetDisplayName(MethodInfo methodInfo, object[] data)
        {
            if ((bool) data[0])
                return "NetCore";

            return "NetFramework";
        }
    }
}
