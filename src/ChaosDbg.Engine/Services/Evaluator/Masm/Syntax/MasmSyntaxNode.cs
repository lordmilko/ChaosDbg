using System.Diagnostics;

namespace ChaosDbg.Evaluator.Masm.Syntax
{
    [DebuggerDisplay("[{Kind}] {ToString(),nq}")]
    abstract class MasmSyntaxNode
    {
        public MasmSyntaxKind Kind { get; }

        protected MasmSyntaxNode(MasmSyntaxKind kind)
        {
            Kind = kind;
        }

        public abstract long Accept(MasmEvaluatorVisitor visitor);
    }
}
