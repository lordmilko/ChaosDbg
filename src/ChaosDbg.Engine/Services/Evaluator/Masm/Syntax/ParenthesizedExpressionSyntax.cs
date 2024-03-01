namespace ChaosDbg.Evaluator.Masm.Syntax
{
    class ParenthesizedExpressionSyntax : ExpressionSyntax
    {
        public MasmSyntaxToken OpenParenToken { get; }

        public ExpressionSyntax Expression { get; }

        public MasmSyntaxToken CloseParenToken { get; }

        public ParenthesizedExpressionSyntax(MasmSyntaxToken openParenToken, ExpressionSyntax expression, MasmSyntaxToken closeParenToken) : base(MasmSyntaxKind.ParenthesizedExpression)
        {
            OpenParenToken = openParenToken;
            Expression = expression;
            CloseParenToken = closeParenToken;
        }

        public override long Accept(MasmEvaluatorVisitor visitor) =>
            visitor.VisitParenthesizedExpression(this);

        public override string ToString()
        {
            return $"{OpenParenToken}{Expression}{CloseParenToken}";
        }
    }
}
