using System;
using ChaosDbg.Cordb;

namespace chaos.Cordb.Commands
{
    class ControlCommands
    {
        private CordbEngine engine;

        public ControlCommands(CordbEngine engine)
        {
            this.engine = engine;
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
