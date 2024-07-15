using System;
using ChaosDbg;

namespace chaos.Cordb.Commands
{
    class ExamineCommands : CommandBase
    {
        public ExamineCommands(IConsole console, DebugEngineProvider engineProvider) : base(console, engineProvider)
        {
        }

        public void Examine(
            [Argument] string pattern)
        {
            throw new NotImplementedException();
        }
    }
}
