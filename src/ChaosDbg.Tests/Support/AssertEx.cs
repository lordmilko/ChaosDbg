using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    public static class AssertEx
    {
        public static void Throws<T>(Action action, string message, bool checkMessage = true) where T : Exception
        {
            try
            {
                action();

                Assert.Fail($"Expected an assertion of type {typeof(T)} to be thrown, however no exception occurred");
            }
            catch (T ex)
            {
                if (checkMessage)
                    Assert.IsTrue(ex.Message.Contains(message), $"Exception message '{ex.Message}' did not contain string '{message}'");
            }
            catch (Exception ex) when (!(ex is AssertFailedException))
            {
                throw;
            }
        }

        public static void ArrayEqual<T1, T2>(T1[] expected, T2[] actual, string what, Func<T2, object> selector = null)
        {
            Assert.AreEqual(expected.Length, actual.Length, $"{what} expected array length was incorrect. Expected: {string.Join(", ", expected)}. Actual: {string.Join(", ", actual)}");

            for (var i = 0; i < expected.Length; i++)
            {
                var expectedElm = expected[i].ToString();
                var actualElm = (selector == null ? actual[i] : selector(actual[i])).ToString();

                Assert.AreEqual(expectedElm, actualElm, $"{what} value at index {i} was incorrect");
            }
        }
    }
}
