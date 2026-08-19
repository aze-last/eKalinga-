using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AttendanceShiftingManagement.Helpers
{
    public sealed class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNullOrEmpty = value == null || (value is string s && string.IsNullOrWhiteSpace(s));
            string? paramString = parameter?.ToString();
            bool invert = !string.IsNullOrEmpty(paramString) &&
                         (paramString.Equals("Inverse", StringComparison.OrdinalIgnoreCase) ||
                          paramString.Equals("Inverted", StringComparison.OrdinalIgnoreCase));

            if (invert)
            {
                return isNullOrEmpty ? Visibility.Visible : Visibility.Collapsed;
            }

            return isNullOrEmpty ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
