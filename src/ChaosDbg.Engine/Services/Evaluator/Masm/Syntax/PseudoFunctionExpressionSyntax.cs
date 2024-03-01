namespace ChaosDbg.Evaluator.Masm.Syntax
{
    class PseudoFunctionExpressionSyntax : ExpressionSyntax
    {
        public MasmSyntaxToken Token { get; }

        public ExpressionSyntax Operand { get; }

        public PseudoFunctionExpressionSyntax(MasmSyntaxKind kind, MasmSyntaxToken token, ExpressionSyntax operand) : base(kind)
        {
            Token = token;
            Operand = operand;
        }

        public override long Accept(MasmEvaluatorVisitor visitor) =>
            visitor.VisitPseudoFuntionExpression(this);

        public override string ToString()
        {
            if (Operand is ParenthesizedExpressionSyntax)
                return $"{Token}{Operand}";

            return $"{Token} {Operand}";
        }
    }
}
