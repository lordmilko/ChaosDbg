using System;
using System.ComponentModel;
using System.Globalization;

namespace ChaosDbg
{
    /// <summary>
    /// Specifies a length that has been applied to a <see cref="DockContainer"/> for determining its relative size
    /// within a parent <see cref="SplitterItemsControl"/>.
    /// </summary>
    [TypeConverter(typeof(SplitPaneLengthConverter))]
    public struct SplitPaneLength : IEquatable<SplitPaneLength>
    {
        //This type is modelled on the design of GridLength

        public double Value { get; }

        public SplitPaneLengthType Type { get; }

        public bool IsProportional => Type == SplitPaneLengthType.Proportional;

        public bool IsFill => Type == SplitPaneLengthType.Fill;

        public SplitPaneLength(double value)
        {
            Value = value;
            Type = SplitPaneLengthType.Proportional;
        }

        public SplitPaneLength(double value, SplitPaneLengthType type)
        {
            Value = value;
            Type = type;
        }

        public static bool operator ==(SplitPaneLength left, SplitPaneLength right) => left.Type == right.Type && left.Value == right.Value;

        public static bool operator !=(SplitPaneLength left, SplitPaneLength right) => left.Type != right.Type || left.Value != right.Value;

        public override bool Equals(object obj)
        {
            if (obj is SplitPaneLength l)
                return this == l;

            return false;
        }

        public bool Equals(SplitPaneLength other) => this == other;

        public override int GetHashCode() => (int) Value + (int) Type;

        public override string ToString() => SplitPaneLengthConverter.ToString(this, CultureInfo.InvariantCulture);
    }
}
