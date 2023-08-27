using System.Windows;

namespace ChaosDbg.Text
{
    class UiTextRunCollection : UiTextLineOrCollection, IUiTextRun
    {
        public ITextRunCollection Collection => (ITextRunCollection) base.Owner;

        public override double Width
        {
            get
            {
                var width = base.Width;

                if (Collection.Style != null)
                    width += Collection.Style.Margin.Right;

                return width;
            }
        }

        public Point Position { get; }

        public UiTextRunCollection(ITextRunCollection collection, Point position) : base(collection)
        {
            Position = position;
        }
    }
}
