using System;

namespace ChaosDbg.Text
{
    /// <summary>
    /// Represents a chunk of text in an <see cref="ITextLine"/>.
    /// </summary>
    class TextRun : ITextRun
    {
        public string Text { get; }

        public TextRun(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            Text = text;
        }

        public override string ToString()
        {
            return Text;
        }
    }
}
