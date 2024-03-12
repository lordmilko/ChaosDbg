namespace ChaosDbg.Evaluator.Masm.Syntax
{
    class SymbolModuleQualifiedExpressionSyntax : ExpressionSyntax
    {
        public MasmSyntaxToken Module { get; }

        public MasmSyntaxToken ExclamationMark { get; }

        public ExpressionSyntax Expression { get; }

        public SymbolModuleQualifiedExpressionSyntax(MasmSyntaxToken module, MasmSyntaxToken exclamationMark, ExpressionSyntax expression) : base(MasmSyntaxKind.SymbolModuleExpression)
        {
            Module = module;
            ExclamationMark = exclamationMark;
            Expression = expression;
        }

        public override long Accept(MasmEvaluatorVisitor visitor) =>
            visitor.VisitSymbolModuleQualifiedExpression(this);

        public override string ToString()
        {
            return $"{Module}{ExclamationMark}{Expression}";
        }
    }
}
