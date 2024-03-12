using System;
using ChaosDbg;

namespace chaos
{
    public class PhysicalConsole : IConsole
    {
        private bool isWriteProtected;
        private int protectedCursorTop;

        private object objLock = new object();

        public void Write(object value, params object[] arg) =>
            WriteProtect(() =>
            {
                if (arg.Length == 0)
                    Console.Write(value);
                else
                    Console.Write(value?.ToString() ?? string.Empty, arg);
            });

        public void WriteLine(object value, params object[] arg) =>
            WriteProtect(() =>
            {
                if (arg.Length == 0)
                    Console.WriteLine(value);
                else
                    Console.WriteLine(value?.ToString() ?? string.Empty, arg);
            });

        public void WriteColorLine(object value, ConsoleColor? color)
        {
            WriteColor(value, color);
            this.WriteLine();
        }

        public void WriteColor(object value, ConsoleColor? color)
        {
            if (color == null)
                Write(value);
            else
            {
                var original = Console.ForegroundColor;

                try
                {
                    Console.ForegroundColor = color.Value;

                    Write(value);
                }
                finally
                {
                    Console.ForegroundColor = original;
                }
            }
        }

        public string ReadLine() => Console.ReadLine();

        public void RegisterInterruptHandler(ConsoleCancelEventHandler handler)
        {
            Console.CancelKeyPress += handler;
        }

        private void WriteProtect(Action action)
        {
            if (!isWriteProtected)
            {
                action();
                return;
            }

            lock (objLock)
            {
                //Write the line above the current line we're showing a prompt + requesting for input

                int cursorLeft = Console.CursorLeft;
                Console.MoveBufferArea(0, protectedCursorTop, Console.WindowWidth, 1, 0, protectedCursorTop + 1);
                Console.SetCursorPosition(0, protectedCursorTop);

                action();

                Console.SetCursorPosition(cursorLeft, protectedCursorTop + 1);
                protectedCursorTop++;
            }
        }

        public void EnterWriteProtection()
        {
            isWriteProtected = true;
            protectedCursorTop = Console.CursorTop;

            //Normally, you would enter write protection while still on the current line.
            //If you've gone to a new line, newly written lines won't be written above
            //the empty line properly, so move to above the previous line with content
            if (Console.CursorLeft == 0)
                protectedCursorTop--;
        }

        public void ExitWriteProtection()
        {
            isWriteProtected = false;
            protectedCursorTop = default;
        }

        public void WriteAndProtect(object value)
        {
            lock (objLock)
            {
                Write(value);

                EnterWriteProtection();
            }
        }
    }
}
