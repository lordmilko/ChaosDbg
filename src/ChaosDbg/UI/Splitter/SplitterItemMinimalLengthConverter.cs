using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace ChaosDbg
{
    public class SplitterItemMinimalLengthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Any(v => v == DependencyProperty.UnsetValue))
                return 0.0;

            var dockContainerHeightOrWidth = (double) values[0];
            var splitterGripSize = (double) values[1];
            var isLast = (bool) values[2];

            //Each SplitterItem contains a DockContainer and a splitter grip. The last
            //SplitterItem within a parent SplitterPanel however has its splitter grip hidden.

            if (isLast)
                return dockContainerHeightOrWidth;

            return dockContainerHeightOrWidth + splitterGripSize;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException($"Converting a {nameof(SplitterItem)} back into its source components is not supported");
        }
    }
}
