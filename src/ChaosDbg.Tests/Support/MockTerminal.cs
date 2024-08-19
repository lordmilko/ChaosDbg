using System;
using System.Collections.Generic;
using ChaosDbg.Terminal;
using ChaosLib;

namespace ChaosDbg.Tests
{
    class MockTerminal : ITerminal
    {
        private CONSOLE_SCREEN_BUFFER_INFO screenBufferInfo = new CONSOLE_SCREEN_BUFFER_INFO
        {
            dwSize = new COORD
            {
                X = 100,
                Y = 100
            }
        };
        private ConsoleMode inputMode = (ConsoleMode) 0x1f7; //This is the default value
        private ConsoleMode outputMode = ConsoleMode.ENABLE_PROCESSED_INPUT | ConsoleMode.ENABLE_LINE_INPUT;
        private ConsoleCtrlHandlerRoutine handler;

        public List<string> Output { get; } = new List<string>();

        public Func<string> OnReadConsole { get; set; }

        public void InvokeCtrlC()
        {
            handler(ConsoleControlType.CTRL_C_EVENT);
        }

        public void EnterWriteProtection()
        {
            //throw new NotImplementedException();
        }

        public void ExitWriteProtection()
        {
            //throw new NotImplementedException();
        }

        public Action<int> OnProtectedWrite { get; set; }

        public void WriteConsole(string value, bool newLine)
        {
            if (newLine)
                Output.Add(value + Environment.NewLine);
            else
                Output.Add(value);
        }

        public void LockProtection(Action action)
        {
            action();
        }

        public string ReadConsole(int charsToRead, System.Span<char> buffer, int charactersToRead, bool endOnTab, out int keyState)
        {
            var result = OnReadConsole();
            keyState = default;

            return result;
        }

        public void FillConsoleOutputCharacter(char c, int length, COORD coord)
        {
            throw new NotImplementedException();
        }

        public ConsoleMode GetOutputConsoleMode() => outputMode;

        public void SetOutputConsoleMode(ConsoleMode mode)
        {
            throw new NotImplementedException();
        }

        public ConsoleMode GetInputConsoleMode() => inputMode;

        public void SetInputConsoleMode(ConsoleMode mode) => inputMode = mode;

        public CONSOLE_SCREEN_BUFFER_INFO GetConsoleScreenBufferInfo() => screenBufferInfo;

        public void SetConsoleTextAttribute(ConsoleBufferAttributes attributes)
        {
            throw new NotImplementedException();
        }

        public void SetCursorPosition(int x, int y)
        {
            screenBufferInfo.dwCursorPosition = new COORD
            {
                X = (short) x,
                Y = (short) y
            };
        }

        public void FlushConsoleInputBuffer()
        {
            throw new NotImplementedException();
        }

        public void AddBreakHandler(ConsoleCtrlHandlerRoutine handler)
        {
            this.handler = handler;
        }

        public void RemoveBreakHandler(ConsoleCtrlHandlerRoutine handler)
        {
            throw new NotImplementedException();
        }

        public void SendInput(INPUT[] inputs, bool activateWindow)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
        }
    }
}
