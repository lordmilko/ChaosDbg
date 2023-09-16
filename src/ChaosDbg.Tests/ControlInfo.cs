using System;
using System.Windows;

namespace ChaosDbg.Tests
{
    class ControlInfo
    {
        public Type Type { get; }

        public virtual double Width { get; }

        public virtual double Height { get; }

        public Action<IPaneItem> Verify { get; }

        public ControlInfo[] Children { get; }

        public Rect Bounds { get; protected set; }

        public ControlInfo Parent { get; private set; }

        public ControlInfo(Type type, double width, double height, Action<IPaneItem> verify, ControlInfo[] children)
        {
            Type = type;
            Width = width;
            Height = height;
            Verify = verify;
            Children = children;

            foreach (var child in children)
                child.Parent = this;

            if (!(this is InheritedControlInfo))
                Arrange(0, 0);
        }

        public virtual void Arrange(double x, double y)
        {
            Bounds = new Rect(x, y, Width, Height);

            foreach (var child in Children)
            {
                child.Arrange(x, y);

                if (child.Width != Width)
                    x += child.Width;

                if (child.Height != Height)
                    y += child.Height;
            }
        }

        public override string ToString()
        {
            return $"[{Type.Name}] Width = {Width}, Height = {Height}";
        }
    }
}
