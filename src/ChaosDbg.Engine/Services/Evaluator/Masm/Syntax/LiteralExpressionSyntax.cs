namespace ChaosDbg.Evaluator.Masm.Syntax
{
    class LiteralExpressionSyntax : ExpressionSyntax
    {
        public MasmSyntaxToken Token { get; }

        public LiteralExpressionSyntax(MasmSyntaxKind kind, MasmSyntaxToken token) : base(kind)
        {
            Token = token;
        }

        public override long Accept(MasmEvaluatorVisitor visitor) =>
            visitor.VisitLiteralExpression(this);

        public override string ToString()
        {
            return Token.ToString();
        }
    }
}
