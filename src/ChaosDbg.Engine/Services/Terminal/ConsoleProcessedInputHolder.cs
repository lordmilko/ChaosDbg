using System;
using ChaosLib;

namespace ChaosDbg.Terminal
{
    class ConsoleProcessedInputHolder : IDisposable
    {
        private ITerminal terminal;
        private ConsoleMode oldMode;
        private bool shouldExecute;

        public ConsoleProcessedInputHolder(ITerminal terminal, bool shouldExecute)
        {
            this.terminal = terminal;

            if (shouldExecute)
            {
                oldMode = terminal.GetInputConsoleMode();

                var newMode = oldMode |= ConsoleMode.ENABLE_PROCESSED_INPUT;

                if (oldMode == newMode)
                    terminal.SetInputConsoleMode(newMode);
                else
                    shouldExecute = false;
            }

            this.shouldExecute = shouldExecute;
        }

        public void Dispose()
        {
            if (shouldExecute)
                terminal.SetInputConsoleMode(oldMode);
        }
    }
}
