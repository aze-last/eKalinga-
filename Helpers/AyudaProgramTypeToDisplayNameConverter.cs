using System;
using System.Globalization;
using System.Windows.Data;
using AttendanceShiftingManagement.Models;

namespace AttendanceShiftingManagement.Helpers;

public sealed class AyudaProgramTypeToDisplayNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is AyudaProgramType programType)
        {
            return programType switch
            {
                AyudaProgramType.GeneralPurpose => "Project Distribution",
                AyudaProgramType.CashForWork => "Cash-for-Work Program (Wages / Attendance)",
                AyudaProgramType.Seminar => "Seminar & Training (Attendance-based)",
                AyudaProgramType.AssistanceCase => "Individual Assistance Case",
                _ => value.ToString() ?? string.Empty
            };
        }

        if (value is string strValue)
        {
            return strValue switch
            {
                "GeneralPurpose" => "Project Distribution",
                "CashForWork" => "Cash-for-Work Program (Wages / Attendance)",
                "Seminar" => "Seminar & Training (Attendance-based)",
                "AssistanceCase" => "Individual Assistance Case",
                _ => strValue
            };
        }

        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
