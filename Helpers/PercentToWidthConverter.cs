using System;
using System.Globalization;
using System.Windows.Data;

namespace AttendanceShiftingManagement.Helpers
{
    public class PercentToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2)
            {
                return 0.0;
            }

            if (values[0] is double percent && values[1] is double totalWidth)
            {
                return totalWidth * (percent / 100.0);
            }

            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
