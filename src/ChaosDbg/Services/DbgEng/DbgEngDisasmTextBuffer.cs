using System;
using ChaosDbg.Render;
using ChaosDbg.Scroll;
using ChaosDbg.Text;
using ChaosDbg.Theme;

namespace ChaosDbg.DbgEng
{
    public class DbgEngDisasmTextBuffer : ITextBuffer, ILogicalScrollContent
    {
        public event EventHandler<EventArgs> UpdateBuffer;

        public void RaiseUpdateBuffer(EventArgs args) => EventExtensions.HandleEvent(UpdateBuffer, args);

        public Font Font { get; }

        public int LineCount => module.Size;

        private DbgEngModule module;

        private CodeNavigator navigator;

        internal DbgEngDisasmTextBuffer(
            Font font,
            DbgEngModule module,
            CodeNavigator navigator)
        {
            Font = font;
            this.module = module;
            this.navigator = navigator;
        }

        private int startIndex;
        private ITextLine[] lines;

        public void PrepareLines(int startIndex, int endIndex)
        {
            this.startIndex = startIndex;
            lines = navigator.GetLines(startIndex, endIndex);
        }

        public ITextLine GetLine(int lineIndex, LineMode lineMode)
        {
            if (lineMode == LineMode.Absolute)
                lineIndex -= startIndex;

            if (lineIndex >= lines.Length)
                return new TextLine(new TextRun(string.Empty));

            return lines[lineIndex];
        }

        public IRenderable ToRenderable() => new UiTextBuffer(this);

        public long SeekVertical(long newOffset) =>
            navigator.SeekVertical(newOffset);

        public long StepUp(int count) =>
            navigator.StepUp(count);

        public long StepDown(int count) =>
            navigator.StepDown(count);
    }
}
