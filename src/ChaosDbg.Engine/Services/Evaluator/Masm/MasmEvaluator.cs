namespace ChaosDbg.Evaluator.Masm
{
    public static class MasmEvaluator
    {
        public static long Evaluate(string expr, IEvaluatorContext context)
        {
            var (tokens, lexErrors) = MasmLexer.Lex(expr);

            //If the error requested to include the original expression, fill it in
            if (lexErrors.Length > 0)
                throw new InvalidExpressionException(string.Format(lexErrors[0], expr));

            var (syntax, parseErrors) = MasmParser.Parse(tokens);

            //If the error requested to include the original expression, fill it in
            if (parseErrors.Length > 0)
                throw new InvalidExpressionException(string.Format(parseErrors[0], expr));

            var visitor = new MasmEvaluatorVisitor(context);
            var result = syntax.Accept(visitor);

            return result;
        }
    }
}
