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

        protected void Error(string message) => Console.WriteColorLine(message, System.ConsoleColor.Red);
    }
}
