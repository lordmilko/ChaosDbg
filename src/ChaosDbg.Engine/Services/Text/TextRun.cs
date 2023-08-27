using System;
using System.Diagnostics;

namespace ChaosDbg.Text
{
    /// <summary>
    /// Represents a chunk of text in an <see cref="ITextLine"/>.
    /// </summary>
    public interface ITextRun
    {
        /// <summary>
        /// Gets the text that this run encapsulates.
        /// </summary>
        string Text { get; }

        /// <summary>
        /// Gets the styles that are used to modify the basic look and layout of this run.
        /// </summary>
        ITextStyle Style { get; }

        ITextRunDecoration[] Decorations { get; set; }
    }

    /// <summary>
    /// Represents a chunk of text in an <see cref="ITextLine"/>.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    class TextRun : ITextRun
    {
        private string DebuggerDisplay => Name != null ? $"[{Name}] {Text}" : Text;

        public string Name { get; set; }

        public string Text { get; }

        public ITextStyle Style { get; set; }

        public ITextRunDecoration[] Decorations { get; set; }

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
