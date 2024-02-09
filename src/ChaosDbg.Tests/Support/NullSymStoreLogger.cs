using System;
using ChaosDbg.SymStore;

namespace ChaosDbg.Tests
{
    class NullSymStoreLogger : ISymStoreLogger
    {
        public void WriteLine(string message)
        {
        }

        public void WriteLine(string format, params object[] arguments)
        {
        }

        public void Information(string message)
        {
        }

        public void Information(string format, params object[] arguments)
        {
        }

        public void Warning(string message)
        {
            throw new NotImplementedException();
        }

        public void Warning(string format, params object[] arguments)
        {
            throw new NotImplementedException();
        }

        public void Error(string message)
        {
            throw new NotImplementedException();
        }

        public void Error(string format, params object[] arguments)
        {
            throw new NotImplementedException();
        }

        public void Verbose(string message)
        {
        }

        public void Verbose(string format, params object[] arguments)
        {
        }
    }
}
