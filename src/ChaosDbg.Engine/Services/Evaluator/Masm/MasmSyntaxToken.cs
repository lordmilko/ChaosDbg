using System.Diagnostics;

namespace ChaosDbg.Evaluator.Masm
{
    [DebuggerDisplay("[{Kind}] {Text,nq}")]
    public struct MasmSyntaxToken
    {
        public MasmSyntaxKind Kind { get; }

        public string Text { get; }

        public object Value { get; }

        public MasmSyntaxToken(MasmSyntaxKind kind, string text, object value)
        {
            Kind = kind;
            Text = text;
            Value = value;
        }

        public override string ToString()
        {
            return Text;
        }
    }
}
