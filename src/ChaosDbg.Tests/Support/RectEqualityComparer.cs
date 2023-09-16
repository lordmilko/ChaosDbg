using System;
using System.Collections.Generic;
using System.Windows;

namespace ChaosDbg.Tests
{
    class RectEqualityComparer : IEqualityComparer<Rect>
    {
        public static readonly RectEqualityComparer Instance = new RectEqualityComparer();

        public bool Equals(Rect x, Rect y)
        {
            bool eq(double a, double b) => Math.Abs(a - b) < 0.00000000001;

            if (!eq(x.X, y.X))
                return false;

            if (!eq(x.Y, y.Y))
                return false;

            if (!eq(x.Width, y.Width))
                return false;

            if (!eq(x.Height, y.Height))
                return false;

            return true;
        }

        public int GetHashCode(Rect obj) => obj.GetHashCode();
    }
}
