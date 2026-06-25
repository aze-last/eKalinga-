namespace AttendanceShiftingManagement.Tests;

public sealed class ProjectDistributionPageBindingTests
{
    [Fact]
    public void ProjectDistributionPage_BindsPaginatedThreeColumnManualDistributionLayout()
    {
        var pagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "ProjectDistributionPage.xaml"));

        var xaml = File.ReadAllText(pagePath);

        Assert.Contains("Text=\"PROJECT DISTRIBUTION\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ProgramSummaries}\"", xaml, StringComparison.Ordinal);
        
        Assert.Contains("Command=\"{Binding OpenCreateProjectPanelCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding PendingBeneficiaries}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding PendingPaginationText}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding PrevPendingPageCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding NextPendingPageCommand}\"", xaml, StringComparison.Ordinal);

        Assert.Contains("Text=\"{Binding SelectedPendingBeneficiary.FullName}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding SelectedPendingDigitalIdCardNumber}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding ConfirmReleaseCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"ATTENDANCE PIN\"", xaml, StringComparison.Ordinal);

        Assert.Contains("Text=\"RELEASED\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ReleasedClaims}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ReleasedPaginationText}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding PrevReleasedPageCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding NextReleasedPageCommand}\"", xaml, StringComparison.Ordinal);
    }
}
