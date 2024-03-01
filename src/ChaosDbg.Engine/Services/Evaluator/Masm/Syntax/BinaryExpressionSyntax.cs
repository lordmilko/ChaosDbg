namespace ChaosDbg.Evaluator.Masm.Syntax
{
    class BinaryExpressionSyntax : ExpressionSyntax
    {
        public ExpressionSyntax Left { get; }

        public MasmSyntaxToken OperatorToken { get; }

        public ExpressionSyntax Right { get; }

        public BinaryExpressionSyntax(MasmSyntaxKind kind, ExpressionSyntax left, MasmSyntaxToken operatorToken, ExpressionSyntax right) : base(kind)
        {
            Left = left;
            OperatorToken = operatorToken;
            Right = right;
        }

        public override long Accept(MasmEvaluatorVisitor visitor) =>
            visitor.VisitBinaryExpression(this);

        public override string ToString()
        {
            return $"{Left} {OperatorToken} {Right}";
        }
    }
}
