using System;

namespace ChaosDbg.Tests
{
    class InheritedControlInfo : ControlInfo
    {
        public override double Width => Parent.Width;

        public override double Height => Parent.Height;

        public InheritedControlInfo(Type type, Action<IPaneItem> verify, ControlInfo[] children) : base(type, 0, 0, verify, children)
        {
        }
    }
}
