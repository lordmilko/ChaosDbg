namespace ChaosDbg.Evaluator.Masm.Syntax
{
    class PrefixUnaryExpressionSyntax : ExpressionSyntax
    {
        public MasmSyntaxToken OperatorToken { get; }

        public ExpressionSyntax Operand { get; }

        public PrefixUnaryExpressionSyntax(MasmSyntaxKind kind, MasmSyntaxToken operatorToken, ExpressionSyntax operand) : base(kind)
        {
            OperatorToken = operatorToken;
            Operand = operand;
        }

        public override long Accept(MasmEvaluatorVisitor visitor) =>
            visitor.VisitPrefixUnaryExpression(this);

        public override string ToString()
        {
            return $"{OperatorToken}{Operand}";
        }
    }
}
