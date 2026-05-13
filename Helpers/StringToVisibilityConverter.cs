using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AttendanceShiftingManagement.Helpers
{
    public sealed class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && parameter is string p)
            {
                var options = p.Split('|');
                if (options.Any(opt => string.Equals(s, opt.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    return Visibility.Visible;
                }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
