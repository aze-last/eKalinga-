namespace AttendanceShiftingManagement.Tests;

public sealed class ProjectDistributionPageBindingTests
{
    [Fact]
    public void ProjectDistributionPage_BindsProjectMembershipScannerAndClaimLists()
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

        Assert.Contains("Text=\"Project Distribution\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Programs}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedProgram}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ApprovedBeneficiaries}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedApprovedBeneficiary}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding AddBeneficiaryToProjectCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CreateDistributionScannerSessionCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding DistributionScannerSessionUrl, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ProjectBeneficiaries}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ProjectClaims}\"", xaml, StringComparison.Ordinal);
    }
}
