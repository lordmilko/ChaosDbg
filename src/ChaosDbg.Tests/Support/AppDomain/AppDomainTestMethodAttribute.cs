using System;
using System.Diagnostics;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    /// <summary>
    /// Runs a test in a separate AppDomain.<para/>
    /// This is particularly important for WPF tests, as each control's LoadComponent() method calls into Application.LoadComponent(), which ultimately calls into PreloadedPackages.GetPackage().
    /// While PreloadedPackages does use a lock, you can't have multiple separate applications trying to add and remove packages from the same package source simultaneously.
    /// </summary>
    class AppDomainTestMethodAttribute : TestMethodAttribute
    {
        static AppDomainTestMethodAttribute()
        {
            //Force the cctor to run
            FakeMouse.InstallHook = true;
        }

        public override TestResult[] Execute(ITestMethod testMethod)
        {
            //todo: use appdomain pool

            var item = AppDomainTestPool.Rent();

            var sw = new Stopwatch();
            sw.Start();

            UnitTestOutcome outcome;
            Exception exception = null;

            try
            {
                item.invoker.Invoke(testMethod.TestClassName, testMethod.TestMethodName);
                outcome = UnitTestOutcome.Passed;
            }
            catch (Exception ex)
            {
                if (ex is TargetInvocationException)
                    ex = ex.InnerException;

                outcome = UnitTestOutcome.Failed;
                exception = ex;
            }
            finally
            {
                sw.Stop();
                AppDomainTestPool.Return(item);
            }

            return new[]
            {
                new TestResult
                {
                    Outcome = outcome,
                    TestFailureException = exception,
                    Duration = sw.Elapsed
                }
            };
        }
    }
}
