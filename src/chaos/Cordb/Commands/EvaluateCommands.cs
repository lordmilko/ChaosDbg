using System;
using ChaosDbg.Cordb;
using ChaosDbg.Evaluator.Masm;

namespace chaos.Cordb.Commands
{
    [Command("?")]
    class EvaluateCommands : CommandBase, ICustomCommandParser
    {
        private CordbMasmEvaluatorContext masmEvaluatorContext;

        public EvaluateCommands(
            IConsole console,
            CordbEngineProvider engineProvider,
            CordbMasmEvaluatorContext masmEvaluatorContext) : base(console, engineProvider)
        {
            this.masmEvaluatorContext = masmEvaluatorContext;
        }

        public void Evaluate(
            [Argument] string expr)
        {
            var result = MasmEvaluator.Evaluate(expr, masmEvaluatorContext);

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
