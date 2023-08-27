using System.Collections.Generic;
using System.Windows;

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
                    new TextLine(
                        new TextRunCollection(
                            new TextRun("hello "),
                            new TextRun(i.ToString().PadRight(10))
                        )
                        {
                            Style = new TextStyle
                            {
                                Margin = new Thickness(20)
                            },
                            Decorations = new[]
                            {
                                new RightBorderRunDecoration()
                            }
                        },
                        new TextRunCollection(
                            new TextRun("goodbye "),
                            new TextRun(i.ToString().PadRight(10))
                        )
                        {
                            Decorations = new[]
                            {
                                new RightBorderRunDecoration()
                            }
                        }
                    )
                );
            }

            return new TextBuffer(
                list.ToArray()
            );
        }
    }
}
