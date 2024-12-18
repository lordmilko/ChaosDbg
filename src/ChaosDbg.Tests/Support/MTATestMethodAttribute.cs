﻿using System;
using System.Threading;
using ChaosLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    //Sometimes tests are automatically run as MTA, other times not. As such, we implement a custom TestClassAttribute
    //to ensure that we're running inside an MTA if we're not already
    class MTATestMethodAttribute : TestMethodAttribute
    {
        //The real TestMethodAttribute that was defined on a test
        private TestMethodAttribute attrib;

        public MTATestMethodAttribute()
        {
        }

        public MTATestMethodAttribute(TestMethodAttribute attrib)
        {
            this.attrib = attrib;
        }

        public override TestResult[] Execute(ITestMethod testMethod)
        {
            //Already on an MTA thread; just invoke the method normally
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.MTA)
                return Invoke(testMethod);

            Exception exception = null;

            TestResult[] result = null;
            var thread = new Thread(() =>
            {
                try
                {
                    result = Invoke(testMethod);
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            });
            thread.Name = testMethod.TestMethodName;
            thread.SetApartmentState(ApartmentState.MTA);
            
            thread.Start();
            thread.Join();

            if (exception != null)
                throw exception;

            return result;
        }

        private TestResult[] Invoke(ITestMethod testMethod)
        {
            TestResult[] DoInvoke()
            {
                if (attrib != null)
                    return attrib.Execute(testMethod);

                //Invoke the test method. We will be invoked on an MTA thread if the current thread was STA
                return new[] { testMethod.Invoke(testMethod.Arguments) };
            }

            var result = DoInvoke();

            foreach (var item in result)
            {
                if (item.TestFailureException != null)
                {
                    if (item.TestFailureException.InnerException != null)
                        Log.Error<TestResult>(item.TestFailureException.InnerException, "Test '{testName}' failed: {message}", testMethod.TestMethodName, item.TestFailureException.InnerException.Message);
                    else
                        Log.Error<TestResult>(item.TestFailureException, item.TestFailureException.Message);
                }
            }

            return result;
        }
    }
}
