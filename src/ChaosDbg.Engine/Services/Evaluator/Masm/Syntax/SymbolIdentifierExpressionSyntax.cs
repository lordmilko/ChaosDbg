namespace ChaosDbg.Evaluator.Masm.Syntax
{
    class SymbolIdentifierExpressionSyntax : ExpressionSyntax
    {
        public MasmSyntaxToken Identifier { get; }

        public SymbolIdentifierExpressionSyntax(MasmSyntaxToken identifier) : base(MasmSyntaxKind.SymbolIdentifierExpression)
        {
            Identifier = identifier;
        }

        public override long Accept(MasmEvaluatorVisitor visitor) =>
            visitor.VisitSymbolIdentifierExpression(this);

        public override string ToString()
        {
            return Identifier.ToString();
        }
    }
}
