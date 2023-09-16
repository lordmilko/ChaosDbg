using System.Diagnostics;
using System.Windows;

namespace ChaosDbg
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class IndependentRect
    {
        private string DebuggerDisplay
        {
            get
            {
                if (DPI == 1)
                    return $"{Normal.TopLeft}-{Normal.BottomRight}";

                return $"[1x-{DPI}x] Width = ({Normal.Left}-{Normal.Right}) / ({HiDPI.Left}-{HiDPI.Right}), Height = ({Normal.Top}-{Normal.Bottom}) / ({HiDPI.Top}-{HiDPI.Bottom})";
            }
        }

        /// <summary>
        /// Gets the dimensions of the rectangle as it would exist at 96 DPI (normal scaling)
        /// </summary>
        public Rect Normal { get; }

        /// <summary>
        /// Gets the dimensions of the rectangle as it exists at the current, high DPI scaling.<para/>
        /// If the current DPI is 100% scaling, this value is the same as <see cref="Normal"/>
        /// </summary>
        public Rect HiDPI { get; }

        public double DPI { get; }

        public IndependentRect(Rect normal, Rect hiDpi, double dpi)
        {
            Normal = normal;
            HiDPI = hiDpi;
            DPI = dpi;
        }
    }
}
