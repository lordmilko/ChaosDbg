using System;
using ChaosDbg;

namespace chaos.Cordb.Commands
{
    class ControlCommands : CommandBase
    {
        public ControlCommands(IConsole console, DebugEngineProvider engineProvider) : base(console, engineProvider)
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
