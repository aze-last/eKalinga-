using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AttendanceShiftingManagement.Helpers
{
    public class NumericToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isInverted = parameter?.ToString() == "0" || parameter?.ToString() == "Inverted";

            if (value is int intValue)
            {
                if (isInverted) return intValue == 0 ? Visibility.Visible : Visibility.Collapsed;
                return intValue > 0 ? Visibility.Visible : Visibility.Collapsed;
            }

            if (value is long longValue)
            {
                if (isInverted) return longValue == 0 ? Visibility.Visible : Visibility.Collapsed;
                return longValue > 0 ? Visibility.Visible : Visibility.Collapsed;
            }

            if (value is decimal decimalValue)
            {
                if (isInverted) return decimalValue == 0 ? Visibility.Visible : Visibility.Collapsed;
                return decimalValue > 0 ? Visibility.Visible : Visibility.Collapsed;
            }

            if (value is double doubleValue)
            {
                if (isInverted) return doubleValue == 0 ? Visibility.Visible : Visibility.Collapsed;
                return doubleValue > 0 ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
