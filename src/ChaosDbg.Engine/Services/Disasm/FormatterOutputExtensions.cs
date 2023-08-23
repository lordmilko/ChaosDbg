using Iced.Intel;

namespace ChaosDbg
{
    static class FormatterOutputExtensions
    {
        //For all tokens we append ourselves,
        //we use FormatterTextKind.Text.The FormatterTextKind is ignored by StringOutput
        public static void Write(this FormatterOutput output, string text) =>
            output.Write(text, FormatterTextKind.Text);
    }
}
