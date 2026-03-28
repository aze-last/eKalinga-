namespace AttendanceShiftingManagement.Tests;

public sealed class ProjectDistributionViewModelSourceTests
{
    [Fact]
    public void ProjectDistributionViewModel_UsesFreshContextsAndSkipsAutoReloadWhileBusy()
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
        Assert.Contains("if (!IsBusy)", source, StringComparison.Ordinal);
        Assert.Contains("await using var context = new AppDbContext();", source, StringComparison.Ordinal);
        Assert.Contains("var distributionService = new ProjectDistributionService(context);", source, StringComparison.Ordinal);
        Assert.Contains("var budgetService = new BudgetManagementService(context);", source, StringComparison.Ordinal);
        Assert.Contains("var sessionService = new ScannerSessionService(context);", source, StringComparison.Ordinal);
    }
}
