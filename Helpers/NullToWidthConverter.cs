using System.Globalization;
using System.Windows.Data;

namespace AttendanceShiftingManagement.Helpers
{
    public sealed class NullToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return 0.0;
            }

            if (parameter != null && double.TryParse(parameter.ToString(), out double width))
            {
                return width;
            }

            return double.NaN;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
