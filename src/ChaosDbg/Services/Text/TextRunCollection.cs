using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ChaosDbg.Text
{
    public interface ITextRunCollection : ITextLineOrCollection, ITextRun
    {
        new ITextRunDecoration[] Decorations { get; set; }
    }

    /// <summary>
    /// Represents a logical collection of <see cref="ITextRun"/> elements within a single <see cref="ITextLine"/>.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    class TextRunCollection : ITextRunCollection
    {
        private string DebuggerDisplay => Name != null ? $"[{Name}] {Text}" : Text;

        public string Name { get; set; }

        public string Text => this.GetText();

        public ITextStyle Style { get; set; }

        public ITextRunDecoration[] Decorations { get; set; }

        public ITextBehavior Behavior { get; }

        public IEnumerable<ITextRun> Runs { get; }

        public TextRunCollection(params ITextRun[] runs)
        {
            if (runs == null)
                throw new ArgumentNullException(nameof(runs));

            Runs = runs;
        }

        public override string ToString() => Text;
    }
}
