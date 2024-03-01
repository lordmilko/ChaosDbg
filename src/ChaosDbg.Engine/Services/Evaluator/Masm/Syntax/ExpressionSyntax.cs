namespace ChaosDbg.Evaluator.Masm.Syntax
{
    abstract class ExpressionSyntax : MasmSyntaxNode
    {
        protected ExpressionSyntax(MasmSyntaxKind kind) : base(kind)
        {
        }
    }
}
