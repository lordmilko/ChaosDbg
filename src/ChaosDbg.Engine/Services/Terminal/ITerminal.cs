using System;
using ChaosLib;

namespace ChaosDbg.Terminal
{
    public interface ITerminal
    {
        void EnterWriteProtection();

        void ExitWriteProtection();

        Action<int> OnProtectedWrite { get; set; }

        void WriteConsole(string value, bool newLine);

        void LockProtection(Action action);

        string ReadConsole(int charsToRead, System.Span<char> buffer, int charactersToRead, bool endOnTab, out int keyState);

        public void FillConsoleOutputCharacter(char c, int length, COORD coord);

        ConsoleMode GetOutputConsoleMode();
        void SetOutputConsoleMode(ConsoleMode mode);

        ConsoleMode GetInputConsoleMode();
        void SetInputConsoleMode(ConsoleMode mode);

        CONSOLE_SCREEN_BUFFER_INFO GetConsoleScreenBufferInfo();
        void SetConsoleTextAttribute(ConsoleBufferAttributes attributes);

        void SetCursorPosition(int x, int y);

        void FlushConsoleInputBuffer();

        void AddBreakHandler(ConsoleCtrlHandlerRoutine handler);

        void RemoveBreakHandler(ConsoleCtrlHandlerRoutine handler);

        void SendInput(INPUT[] inputs, bool activateWindow);

        void Clear();
    }
}
