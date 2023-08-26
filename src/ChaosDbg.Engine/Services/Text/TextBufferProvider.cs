using System.Collections.Generic;

namespace ChaosDbg.Text
{
    public interface ITextBufferProvider
    {
        ITextBuffer GetBuffer();
    }

    class TextBufferProvider : ITextBufferProvider
    {
        public ITextBuffer GetBuffer()
        {
            var list = new List<ITextLine>();

            for (var i = 0; i < 50; i++)
            {
                list.Add(
                    new TextLine(new TextRun(i.ToString()))
                );
            }

            return new TextBuffer(
                list.ToArray()
            );
        }
    }
}
