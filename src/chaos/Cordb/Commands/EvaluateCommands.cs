using System;
using ChaosDbg;
using ChaosDbg.Cordb;

namespace chaos.Cordb.Commands
{
    [Command("?")]
    class EvaluateCommands : CommandBase, ICustomCommandParser
    {
        public EvaluateCommands(
            IConsole console,
            CordbEngineProvider engineProvider) : base(console, engineProvider)
        {
        }

        public void Evaluate(
            [Argument] string expr)
        {
            var result = engine.Process.Evaluator.Evaluate(expr);

            Console.WriteLine($"Evaluate expression: {result} = {result:x16}");
        }

        public Action Parse(ArgParser args)
        {
            if (args.Empty)
                throw new NotImplementedException("Displaying help is not implemented");

            var str = args.Eat();

            return () => Evaluate(str);
        }
    }
}
