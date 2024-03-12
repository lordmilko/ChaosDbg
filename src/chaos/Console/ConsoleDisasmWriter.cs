using System;
using ChaosDbg;
using Iced.Intel;

namespace chaos
{
    class ConsoleDisasmWriter : FormatterOutput
    {
        private IConsole console;

        public ConsoleDisasmWriter(IConsole console)
        {
            this.console = console;
        }

        public override void Write(string text, FormatterTextKind kind)
        {
            if (kind == FormatterTextKind.Label)
                console.WriteColor(text, ConsoleColor.Green);
            else if (kind == FormatterTextKind.LabelAddress)
                console.WriteColor(text, ConsoleColor.Yellow);
            else
                console.Write(text);
        }
    }
}
