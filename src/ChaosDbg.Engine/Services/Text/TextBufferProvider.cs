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

            var decorations = new[]
            {
                new RightBorderRunDecoration()
            };

            for (var i = 0; i < 1; i++)
            {

                var arrowsColWidth = 20;
                var addressesColWidth = 8; //Needs to be 16 on x64
                var bytesColWidth = 18;
                var disasmColWidth = 33;

                var arrowsCol = new TextRunCollection(
                    new TextRun("".PadRight(arrowsColWidth))
                )
                {
                    Name = "ArrowsCollection",
                    Style = new TextStyle
                    {
                        Margin = new Thickness(10, 0, 10, 0)
                    }
                };

                var addressesCol = new TextRunCollection(
                    new TextRun("77E61011".PadRight(addressesColWidth))
                )
                {
                    Name = "AddressesCollection",
                    Style = new TextStyle
                    {
                        Margin = new Thickness(3, 0, 6, 0)
                    }
                };

                var bytesCol = new TextRunCollection(
                    new TextRun("1956 77".PadRight(bytesColWidth))
                )
                {
                    Name = "BytesCollection",
                    Style = new TextStyle
                    {
                        Margin = new Thickness(30, 0, 0, 0)
                    }
                };

                //margin: 5 left
                var disasmCol = new TextRunCollection(
                    new TextRun("sbb dword ptr ds:[esi+77],edx".PadRight(disasmColWidth))
                )
                {
                    Name = "DisasmCollection",
                    Style = new TextStyle
                    {
                        Margin = new Thickness(5, 0, 0, 0)
                    }
                };

                var commentsCol = new TextRunCollection();

                var collections = new[]
                {
                    arrowsCol,
                    addressesCol,
                    bytesCol,
                    disasmCol,
                    commentsCol
                };

                for (var j = 0; j < collections.Length; j++)
                {
                    if (j < collections.Length)
                    {
                        collections[j].Decorations = decorations;
                    }
                }

                list.Add(
                    new TextLine(
                        collections
                    )
                );
            }

            return new TextBuffer(
                list.ToArray()
            );
        }
    }
}
