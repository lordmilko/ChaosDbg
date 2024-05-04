using System;
using System.Diagnostics;

namespace ChaosDbg.Tests
{
    class MockConsole : IConsole
    {
        public void Write(object value, params object[] arg)
        {
            throw new NotImplementedException();
        }

        public void WriteLine(object value, params object[] arg)
        {
            Debug.WriteLine(value?.ToString() ?? string.Empty, arg);
        }

        public void WriteColor(object value, ConsoleColor? color)
        {
            throw new NotImplementedException();
        }

        public void WriteColorLine(object value, ConsoleColor? color)
        {
            throw new NotImplementedException();
        }

        public string ReadLine()
        {
            throw new NotImplementedException();
        }

        public void RegisterInterruptHandler(ConsoleCancelEventHandler handler)
        {
        }

        public void EnterWriteProtection()
        {
            throw new NotImplementedException();
        }

        public void ExitWriteProtection()
        {
            throw new NotImplementedException();
        }

        public void WriteAndProtect(object value)
        {
            throw new NotImplementedException();
        }
    }
}
