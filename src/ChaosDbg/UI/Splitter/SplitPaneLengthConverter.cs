using System;
using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Globalization;

namespace ChaosDbg
{
    /// <summary>
    /// Represents a <see cref="TypeConverter"/> used to convert string values used in XAML to <see cref="SplitPaneLength"/> objects.
    /// </summary>
    internal class SplitPaneLengthConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            switch (Type.GetTypeCode(sourceType))
            {
                case TypeCode.String:
                case TypeCode.Decimal:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return true;
                default:
                    return false;
            }
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType) => destinationType == typeof(InstanceDescriptor) || destinationType == typeof(string);

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value != null)
            {
                if (value is string s)
                    return FromString(s, culture);

                var d = Convert.ToDouble(value, culture);

                if (double.IsNaN(d))
                    d = 1.0;

                return new SplitPaneLength(d, SplitPaneLengthType.Proportional);
            }

            throw GetConvertFromException(null);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == null)
                throw new ArgumentNullException(nameof(destinationType));

            if (value is SplitPaneLength l)
            {
                if (destinationType == typeof(string))
                    return ToString(l, culture);

                if (destinationType == typeof(InstanceDescriptor))
                {
                    var ctor = typeof(SplitPaneLength).GetConstructor(new[] { typeof(double), typeof(SplitPaneLengthType) });
                    return new InstanceDescriptor(ctor, new object[] { l.Value, l.Type });
                }
            }

            throw GetConvertToException(value, destinationType);
        }

        private SplitPaneLength FromString(string str, CultureInfo culture)
        {
            str = str.Trim();

            if (str == "*")
                return new SplitPaneLength(1.0, SplitPaneLengthType.Fill);

            return new SplitPaneLength(Convert.ToDouble(str, culture), SplitPaneLengthType.Proportional);
        }

        internal static string ToString(SplitPaneLength length, CultureInfo culture) =>
            length.Type == SplitPaneLengthType.Fill ? "*" : length.Value.ToString(culture);
    }
}
