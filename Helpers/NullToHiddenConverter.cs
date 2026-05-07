using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AttendanceShiftingManagement.Helpers
{
    public class NullToHiddenConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Treat empty or whitespace strings as null
            bool isNull = value == null || (value is string s && string.IsNullOrWhiteSpace(s));
            string? paramString = parameter?.ToString();
            bool invert = !string.IsNullOrEmpty(paramString) &&
                         (paramString.Equals("Inverse", StringComparison.OrdinalIgnoreCase) ||
                          paramString.Equals("Inverted", StringComparison.OrdinalIgnoreCase));

            if (invert)
            {
                return isNull ? Visibility.Visible : Visibility.Hidden;
            }

            return isNull ? Visibility.Hidden : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
