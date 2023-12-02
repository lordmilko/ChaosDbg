using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
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

            TestResult[] result = null;
            var thread = new Thread(() => result = Invoke(testMethod));
            thread.SetApartmentState(ApartmentState.MTA);
            
            thread.Start();
            thread.Join();

            return result;
        }

        private TestResult[] Invoke(ITestMethod testMethod)
        {
            if (attrib != null)
                return attrib.Execute(testMethod);

            //Invoke the test method. We will be invoked on an MTA thread if the current thread was STA
            return new[] { testMethod.Invoke(null) };
        }
    }
}
