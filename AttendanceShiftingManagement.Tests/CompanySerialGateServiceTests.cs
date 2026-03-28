using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class CompanySerialGateServiceTests
{
    [Fact]
    public void ValidateOrBind_WhenDatabaseIsUnregistered_BindsCurrentCompanySerial()
    {
        using var context = TestDbContextFactory.CreateContext();

        var result = CompanySerialGateService.ValidateOrBind(
            context,
            new SystemProfileSettingsModel
            {
                SystemName = "Barangay Ayuda System",
                Owner = "Barangay San Isidro",
                InstallSerial = "bas-20260327-ab12"
            });

        Assert.True(result.IsSuccess);
        Assert.True(result.WasBoundToDatabase);

        var registration = Assert.Single(context.SystemRegistrations);
        Assert.Equal("BAS-20260327-AB12", registration.CompanySerialNumber);
        Assert.Equal("Barangay San Isidro", registration.CompanyName);
    }

    [Fact]
    public void ValidateOrBind_WhenCompanySerialMatchesExistingRegistration_Succeeds()
    {
        using var context = TestDbContextFactory.CreateContext();
        context.SystemRegistrations.Add(new SystemRegistration
        {
            CompanySerialNumber = "BAS-20260327-AB12",
            CompanyName = "Barangay San Isidro"
        });
        context.SaveChanges();

        var result = CompanySerialGateService.ValidateOrBind(
            context,
            new SystemProfileSettingsModel
            {
                Owner = "Barangay San Isidro",
                InstallSerial = "BAS-20260327-AB12"
            });

        Assert.True(result.IsSuccess);
        Assert.False(result.WasBoundToDatabase);
        Assert.Single(context.SystemRegistrations);
    }

    [Fact]
    public void ValidateOrBind_WhenCompanySerialDoesNotMatchExistingRegistration_ReturnsFailure()
    {
        using var context = TestDbContextFactory.CreateContext();
        context.SystemRegistrations.Add(new SystemRegistration
        {
            CompanySerialNumber = "BAS-20260327-AB12",
            CompanyName = "Barangay San Isidro"
        });
        context.SaveChanges();

        var result = CompanySerialGateService.ValidateOrBind(
            context,
            new SystemProfileSettingsModel
            {
                Owner = "Barangay Mabini",
                InstallSerial = "BAS-20260327-ZZ99"
            });

        Assert.False(result.IsSuccess);
        Assert.Contains("already assigned", result.Message, StringComparison.OrdinalIgnoreCase);

        var registration = Assert.Single(context.SystemRegistrations);
        Assert.Equal("BAS-20260327-AB12", registration.CompanySerialNumber);
        Assert.Equal("Barangay San Isidro", registration.CompanyName);
    }
}
