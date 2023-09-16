using System.Windows;

namespace ChaosDbg.Tests
{
    class PaneItemCollection : IPaneItem, IVisitorResultCollection<IPaneItem>
    {
        public DependencyObject Element { get; }

        public IPaneItem[] Children { get; }

        public IndependentRect Bounds { get; }

        public PaneItemCollection(IPaneItem[] children)
        {
            Children = children;
        }
    }
}
