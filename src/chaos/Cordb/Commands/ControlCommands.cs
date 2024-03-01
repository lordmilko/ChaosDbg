using System;
using ChaosDbg.Cordb;

namespace chaos.Cordb.Commands
{
    class ControlCommands : CommandBase
    {
        public ControlCommands(IConsole console, CordbEngineProvider engineProvider) : base(console, engineProvider)
        {
        }

        [Command("g")]
        public void Go()
        {
            engine.Continue();
        }

        [Command("exit")]
        public void Exit()
        {
            Environment.Exit(0);
        }
    }
}
