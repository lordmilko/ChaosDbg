namespace ChaosDbg.Evaluator.Masm.Syntax
{
    class QualifiedNameSyntax : ExpressionSyntax
    {
        public ExpressionSyntax Left { get; }

        public ExpressionSyntax Right { get; }

        public MasmSyntaxToken ColonColonToken { get; }

        public QualifiedNameSyntax(ExpressionSyntax left, MasmSyntaxToken colonColonToken, ExpressionSyntax right) : base(MasmSyntaxKind.QualifiedName)
        {
            Left = left;
            ColonColonToken = colonColonToken;
            Right = right;
        }

        public override long Accept(MasmEvaluatorVisitor visitor)
        {
            throw new System.NotImplementedException();
        }

        public override string ToString()
        {
            return $"{Left}{ColonColonToken}{Right}";
        }
    }
}
