using System;

namespace ChaosDbg.Tests
{
    class ExplicitControlInfo : ControlInfo
    {
        private double x;
        private double y;

        public ExplicitControlInfo(Type type, double x, double y, double width, double height, Action<IPaneItem> verify, ControlInfo[] children) : base(type, width, height, verify, children)
        {
            this.x = x;
            this.y = y;
        }

        public override void Arrange(double x, double y)
        {
            base.Arrange(this.x, this.y);
        }
    }
}
