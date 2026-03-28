namespace AttendanceShiftingManagement.Tests;

public sealed class CashForWorkBeneficiarySourceTests
{
    [Fact]
    public void CashForWorkService_AndViewModel_UseBeneficiaryStagingIdentity_InsteadOfHouseholdMembers()
    {
        var servicePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Services",
            "CashForWorkService.cs"));

        var viewModelPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "ViewModels",
            "CashForWorkOcrViewModel.cs"));

        var serviceSource = File.ReadAllText(servicePath);
        var viewModelSource = File.ReadAllText(viewModelPath);

        Assert.Contains("BeneficiaryStagingId", serviceSource, StringComparison.Ordinal);
        Assert.Contains("VerificationStatus.Approved", serviceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("HouseholdMembers", serviceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("participant.HouseholdMember", serviceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("HouseholdMemberId", serviceSource, StringComparison.Ordinal);

        Assert.Contains("EligibleBeneficiaries", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("SelectedEligibleBeneficiary", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("BeneficiaryId", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("CivilRegistryId", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedEligibleMember", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("HouseholdMemberId", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("HouseholdCode", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Purok", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalScannerGatewayService_FindsAttendanceParticipants_ByBeneficiaryStagingIdentity()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Services",
            "LocalScannerGatewayService.cs"));

        var source = File.ReadAllText(sourcePath);

        Assert.Contains("item.BeneficiaryStagingId == lookup.BeneficiaryStagingId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("item.HouseholdMemberId == lookup.HouseholdMemberId.Value", source, StringComparison.Ordinal);
    }
}
