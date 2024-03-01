using System;

namespace chaos
{
    public interface IConsole
    {
        void Write(object value, params object[] arg);

        void WriteLine(object value, params object[] arg);

        public void WriteColor(object value, ConsoleColor? color);

        public void WriteColorLine(object value, ConsoleColor? color);

        string ReadLine();

        void RegisterInterruptHandler(ConsoleCancelEventHandler handler);

        void EnterWriteProtection();

        void ExitWriteProtection();

        void WriteAndProtect(object value);
    }
}
