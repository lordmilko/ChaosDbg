using System.Windows;

namespace ChaosDbg.Text
{
    class UiTextRunCollection : UiTextLineOrCollection, IUiTextRun
    {
        public ITextRunCollection Collection => (ITextRunCollection) base.Owner;

        public Point Position { get; }

        public UiTextRunCollection(ITextRunCollection collection, Point position) : base(collection)
        {
            Position = position;
        }
    }
}
