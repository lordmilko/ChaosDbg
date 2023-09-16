using System;

namespace ChaosDbg.Tests
{
    static class LayoutFactory
    {
        public static ControlInfo Expected<T>(double width, double height, params ControlInfo[] children) =>
            Expected<T>(width, height, null, children);

        public static ControlInfo Expected<T>(double width, double height, Action<T> verify, params ControlInfo[] children)
        {
            Action<IPaneItem> internalVerify = null;

            if (verify != null)
                internalVerify = v => verify((T) v);

            return new ControlInfo(typeof(T), width, height, internalVerify, children);
        }

        public static ControlInfo Inherited<T>(params ControlInfo[] children)
        {
            return new InheritedControlInfo(typeof(T), null, children);
        }

        public static ControlInfo Explicit<T>(double x, double y, double width, double height, params ControlInfo[] children)
        {
            return new ExplicitControlInfo(typeof(T), x, y, width, height, null, children);
        }
    }
}
