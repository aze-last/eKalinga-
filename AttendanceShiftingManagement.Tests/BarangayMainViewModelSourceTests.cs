namespace AttendanceShiftingManagement.Tests;

public sealed class BarangayMainViewModelSourceTests
{
    [Fact]
    public void BarangayMainViewModel_RoutesValidatedBeneficiariesToApprovalWorkspace_WithoutReviewOrGrievanceSections()
    {
        var viewModelPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "ViewModels",
            "BarangayMainViewModel.cs"));

        var source = File.ReadAllText(viewModelPath);

        Assert.Contains("case \"MasterList\":", source, StringComparison.Ordinal);
        Assert.Contains("CurrentSectionTitle = \"Validated Beneficiaries\";", source, StringComparison.Ordinal);
        Assert.Contains("CurrentSectionSubtitle = \"Read the local val_beneficiaries snapshot, keep new rows pending by default, and approve or reject them before downstream use.\";", source, StringComparison.Ordinal);
        Assert.Contains("return new BeneficiaryVerificationPage(_currentUser);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ShowBeneficiariesCommand", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ShowGrievancesCommand", source, StringComparison.Ordinal);
        Assert.DoesNotContain("case \"Beneficiaries\":", source, StringComparison.Ordinal);
        Assert.DoesNotContain("case \"Grievances\":", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BarangayMainViewModel_ExposesDistributionWorkspaceInSidebarRouting()
    {
        var viewModelPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "ViewModels",
            "BarangayMainViewModel.cs"));

        var source = File.ReadAllText(viewModelPath);

        Assert.Contains("ShowDistributionCommand", source, StringComparison.Ordinal);
        Assert.Contains("IsDistributionSelected", source, StringComparison.Ordinal);
        Assert.Contains("case \"Distribution\":", source, StringComparison.Ordinal);
        Assert.Contains("CurrentSectionTitle = \"Project Distribution\";", source, StringComparison.Ordinal);
        Assert.Contains("return new ProjectDistributionPage(_currentUser);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ShowHouseholdRegistryCommand", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IsHouseholdRegistrySelected", source, StringComparison.Ordinal);
        Assert.DoesNotContain("case \"HouseholdRegistry\":", source, StringComparison.Ordinal);
        Assert.DoesNotContain("return new HouseholdRegistryPage();", source, StringComparison.Ordinal);
    }
}
