namespace AttendanceShiftingManagement.Tests;

public sealed class AssistanceCaseManagementPageBindingTests
{
    [Fact]
    public void AssistanceCaseManagementPage_BindsAidRequestSelectorsAndDeleteAction()
    {
        var pagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "AssistanceCaseManagementPage.xaml"));

        var xaml = File.ReadAllText(pagePath);

        Assert.Contains("Text=\"Aid Request\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ValidatedBeneficiaries}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedValidatedBeneficiary}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding AyudaPrograms}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedAyudaProgram}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ReleaseKindOptions}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedReleaseKind}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DeleteCommand}\"", xaml, StringComparison.Ordinal);
    }
}
