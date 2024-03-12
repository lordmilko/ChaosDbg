using ChaosDbg;
using ChaosDbg.Cordb;

namespace chaos.Cordb.Commands
{
    abstract class CommandBase
    {
        protected IConsole Console { get; }

        private CordbEngineProvider engineProvider;
        protected CordbEngine engine => engineProvider.ActiveEngine;

        protected CommandBase(IConsole console, CordbEngineProvider engineProvider)
        {
            Console = console;
            this.engineProvider = engineProvider;
        }

        protected string FormatAddr(long address)
        {
            if (engine.Process.Is32Bit)
                return address.ToString("x8");

            return $"{(address >> 32):x8}`{((int) address):x8}";
        }

        protected void Error(string message) => Console.WriteColorLine(message, System.ConsoleColor.Red);
    }
}
