using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class CrsAgeCalculatorTests
{
    [Fact]
    public void CalculateAge_ReturnsNull_ForNullDate()
    {
        Assert.Null(CrsAgeCalculator.CalculateAge(null));
    }

    [Fact]
    public void CalculateAge_ComputesYears_WithBirthdayAdjustment()
    {
        var today = DateTime.Today;
        // Born exactly 30 years ago today → 30.
        Assert.Equal(30, CrsAgeCalculator.CalculateAge(today.AddYears(-30)));
        // Birthday tomorrow → still 29.
        Assert.Equal(29, CrsAgeCalculator.CalculateAge(today.AddYears(-30).AddDays(1)));
    }

    [Fact]
    public void CalculateAge_ReturnsNull_ForFutureDates()
    {
        Assert.Null(CrsAgeCalculator.CalculateAge(DateTime.Today.AddYears(2)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0000-00-00")]
    [InlineData("not a date")]
    public void CalculateAgeText_ReturnsNull_ForMalformedRawDates(string? raw)
    {
        // Never 0 — a zero would silently misclassify people as newborns
        // in age-based eligibility checks (schema-drift notice, Issue 4).
        Assert.Null(CrsAgeCalculator.CalculateAgeText(raw));
    }

    [Fact]
    public void CalculateAgeText_ParsesIsoDates()
    {
        var dob = DateTime.Today.AddYears(-65);
        var text = CrsAgeCalculator.CalculateAgeText(dob.ToString("yyyy-MM-dd"));
        Assert.Equal("65", text);
    }

    [Fact]
    public void CrsGateway_NeverSelectsRemovedAgeColumn()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "Services", "CRS", "CrsGateway.cs"));
        var source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain(", age,", source, StringComparison.Ordinal);
        Assert.Contains("CrsAgeCalculator.CalculateAgeText", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CrsBeneficiaryImportService_NeverSelectsRemovedAgeColumn()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "Services", "CrsBeneficiaryImportService.cs"));
        var source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("`age`", source, StringComparison.Ordinal);
        Assert.Contains("CrsAgeCalculator.CalculateAgeText", source, StringComparison.Ordinal);
    }
}
