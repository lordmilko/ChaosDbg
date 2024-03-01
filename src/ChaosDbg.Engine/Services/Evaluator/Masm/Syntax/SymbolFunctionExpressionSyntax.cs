namespace ChaosDbg.Evaluator.Masm.Syntax
{
    class SymbolFunctionExpressionSyntax : ExpressionSyntax
    {
        public MasmSyntaxToken Module { get; }

        public MasmSyntaxToken ExclamationMark { get; }

        public MasmSyntaxToken Function { get; }

        public SymbolFunctionExpressionSyntax(MasmSyntaxToken module, MasmSyntaxToken exclamationMark, MasmSyntaxToken function) : base(MasmSyntaxKind.SymbolFunctionExpression)
        {
            Module = module;
            ExclamationMark = exclamationMark;
            Function = function;
        }

        public override long Accept(MasmEvaluatorVisitor visitor) =>
            visitor.VisitSymbolFunctionExpression(this);

        public override string ToString()
        {
            return $"{Module}{ExclamationMark}{Function}";
        }
    }
}
