using System;
using ChaosDbg.Logger;

namespace ChaosDbg.Tests
{
    /// <summary>
    /// Provides facilities for invoking a unit test in another AppDomain.
    /// </summary>
    class AppDomainInvoker : MarshalByRefObject
    {
        public AppDomainInvoker()
        {
            //Ideally, we would like to pass the original domain to us, and callback into it, but this doesn't work - attempting to callback from the remote domain just seems to give us the remote domain again

            GlobalProvider.AllowGlobalProvider = false;
            SerilogLogger.Install();
            FakeMouse.InstallHook = false;
        }

        public void Invoke(string typeName, string methodName)
        {
            var testType = GetType().Assembly.GetType(typeName);
            var testMethod = testType.GetMethod(methodName);

            var testInitialize = testType.GetMethod("TestInitialize");
            var testCleanup = testType.GetMethod("TestCleanup");
            var testContext = testType.GetProperty("TestContext");

            var instance = Activator.CreateInstance(testType);

            testContext?.SetValue(instance, new AppDomainTestContext(methodName));
            testInitialize?.Invoke(instance, Array.Empty<object>());

            testMethod.Invoke(instance, Array.Empty<object>());

            testCleanup?.Invoke(instance, Array.Empty<object>());
        }

        public override object InitializeLifetimeService() => null;
    }
}
