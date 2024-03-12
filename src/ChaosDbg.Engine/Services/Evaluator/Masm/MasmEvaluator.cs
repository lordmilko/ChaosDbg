using System.Linq;

namespace ChaosDbg.Evaluator.Masm
{
    public class MasmEvaluator
    {
        private IEvaluatorContext context;

        public MasmEvaluator(IEvaluatorContext context)
        {
            this.context = context;
        }

        public long Evaluate(string expr)
        {
            if (!TryEvaluate(expr, out var result, out var errors))
                throw new InvalidExpressionException(errors[0]);

            return result;
        }

        public bool TryEvaluate(string expr, out long result, out string[] errors)
        {
            result = default;
            errors = default;

            var (tokens, lexErrors) = MasmLexer.Lex(expr);

            //If the error requested to include the original expression, fill it in
            if (lexErrors.Length > 0)
            {
                errors = lexErrors.Select(v => string.Format(v, expr)).ToArray();
                return false;
            }

            var (syntax, parseErrors) = MasmParser.Parse(tokens);

            //If the error requested to include the original expression, fill it in
            if (parseErrors.Length > 0)
            {
                errors = parseErrors.Select(v => string.Format(v, expr)).ToArray();
                return false;
            }

            var visitor = new MasmEvaluatorVisitor(context);

            try
            {
                result = syntax.Accept(visitor);
                return true;
            }
            catch (InvalidExpressionException ex)
            {
                errors = new[] {ex.Message};
                return false;
            }
        }
    }
}
