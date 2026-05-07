namespace AttendanceShiftingManagement.Tests;

public sealed class ProjectDistributionViewModelSourceTests
{
    [Fact]
    public void ProjectDistributionViewModel_UsesFreshContexts_AndSupportsPaginatedManualDistribution()
    {
        var viewModelPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "ViewModels",
            "ProjectDistributionViewModel.cs"));

        var source = File.ReadAllText(viewModelPath);

        Assert.DoesNotContain("_context = new AppDbContext()", source, StringComparison.Ordinal);
        Assert.Contains("ConfirmReleaseAsync", source, StringComparison.Ordinal);
        Assert.Contains("PendingBeneficiaries.Clear();", source, StringComparison.Ordinal);
        Assert.Contains("ReleasedClaims.Clear();", source, StringComparison.Ordinal);
        Assert.Contains("GetPendingBeneficiariesPaginatedAsync", source, StringComparison.Ordinal);
        Assert.Contains("GetReleasedClaimsPaginatedAsync", source, StringComparison.Ordinal);
        Assert.Contains("ChangePendingPageAsync", source, StringComparison.Ordinal);
        Assert.Contains("ChangeReleasedPageAsync", source, StringComparison.Ordinal);
        Assert.Contains("ProgramSummaries.Clear();", source, StringComparison.Ordinal);
        Assert.Contains("GroupBy(b => b.AyudaProgramId)", source, StringComparison.Ordinal);
        Assert.Contains("ToDictionaryAsync(x => x.ProgramId)", source, StringComparison.Ordinal);
        Assert.Contains("if (!IsBusy)", source, StringComparison.Ordinal);
        Assert.Contains("await using var context = new AppDbContext();", source, StringComparison.Ordinal);
        Assert.Contains("context.AyudaPrograms", source, StringComparison.Ordinal);
        Assert.Contains("context.AyudaProjectBeneficiaries", source, StringComparison.Ordinal);
        Assert.Contains("context.AyudaProjectClaims", source, StringComparison.Ordinal);
    }
}
