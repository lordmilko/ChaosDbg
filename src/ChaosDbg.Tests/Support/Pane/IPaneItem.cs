using System.Windows;

namespace ChaosDbg.Tests
{
    interface IPaneItem
    {
        DependencyObject Element { get; }

        IPaneItem[] Children { get; }

        IndependentRect Bounds { get; }
    }
}
