namespace ChaosDbg.Evaluator.Masm.Syntax
{
    class IdentifierNameSyntax : ExpressionSyntax
    {
        public MasmSyntaxToken Token { get; }

        public IdentifierNameSyntax(MasmSyntaxToken token) : base(MasmSyntaxKind.IdentifierName)
        {
            Token = token;
        }

        public override long Accept(MasmEvaluatorVisitor visitor)
        {
            throw new System.NotImplementedException();
        }

        public override string ToString()
        {
            return Token.ToString();
        }
    }
}
