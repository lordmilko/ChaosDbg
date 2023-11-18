using System;
using System.Collections.Generic;
using ChaosDbg.Render;

namespace ChaosDbg.Text
{
    /// <summary>
    /// Represents a collection of <see cref="ITextRun"/> chunks that are displayed on a single line.
    /// </summary>
    public interface ITextLine : ITextLineOrCollection, IConvertableToRenderable
    {
    }

    /// <summary>
    /// Represents a collection of <see cref="ITextRun"/> chunks that are displayed on a single line.
    /// </summary>
    class TextLine : ITextLine
    {
        public IEnumerable<ITextRun> Runs { get; }
        public ITextRunDecoration[] Decorations { get; set; }

        public TextLine(params ITextRun[] runs)
        {
            if (runs == null)
                throw new ArgumentNullException(nameof(runs));

            Runs = runs;
        }

        public IRenderable ToRenderable() => new UiTextLine(this);

        public override string ToString() => this.GetText();
    }
}
