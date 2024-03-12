using System;
using ChaosDbg;
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

        [Command("p")]
        public void StepOver()
        {
            engine.StepOverNative();
        }

        [Command("t")]
        public void StepInto()
        {
            engine.StepIntoNative();
        }

        [Command("exit")]
        public void Exit()
        {
            Environment.Exit(0);
        }
    }
}
