using VsDock;

namespace ChaosDbg.Tests
{
    class SplitterGripInfo : PaneItem<SplitterGrip>
    {
        public SplitterGripInfo(SplitterGrip element, IPaneItem[] children) : base(element, children)
        {
        }
    }
}
