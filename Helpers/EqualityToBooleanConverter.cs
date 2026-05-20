using System;
using System.Globalization;
using System.Windows.Data;

namespace AttendanceShiftingManagement.Helpers
{
    public sealed class EqualityToBooleanConverter : IValueConverter
    {
        public bool Inverse { get; set; }

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return Inverse ? true : false;
            bool isEqual = value.ToString() == parameter.ToString();
            return Inverse ? !isEqual : isEqual;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return parameter?.ToString() ?? string.Empty;
        }
    }
}
