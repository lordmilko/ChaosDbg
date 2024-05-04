using System.Diagnostics;

namespace ChaosDbg.Evaluator.Cpp
{
    [DebuggerDisplay("[{Kind}] {Text,nq}")]
    public struct CppSyntaxToken
    {
        public CppSyntaxKind Kind { get; }

        public string Text { get; }

        public object Value { get; }

        public CppSyntaxToken(CppSyntaxKind kind, string text, object value)
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