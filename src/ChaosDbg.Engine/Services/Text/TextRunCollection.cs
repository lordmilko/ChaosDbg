using System;
using System.Collections.Generic;

namespace ChaosDbg.Text
{
    public interface ITextRunCollection : ITextLineOrCollection, ITextRun
    {
        new ITextRunDecoration[] Decorations { get; set; }
    }

    /// <summary>
    /// Represents a logical collection of <see cref="ITextRun"/> elements within a single <see cref="ITextLine"/>.
    /// </summary>
    class TextRunCollection : ITextRunCollection
    {
        public string Text => this.GetText();

        public ITextStyle Style { get; set; }

        public ITextRunDecoration[] Decorations { get; set; }

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
