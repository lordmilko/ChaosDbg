using System;
using ChaosDbg;
using ChaosDbg.Cordb;

namespace chaos.Cordb.Commands
{
    class ExamineCommands : CommandBase
    {
        public ExamineCommands(IConsole console, CordbEngineProvider engineProvider) : base(console, engineProvider)
        {
        }

        public void Examine(
            [Argument] string pattern)
        {
            throw new NotImplementedException();
        }
    }
}
