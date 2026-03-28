namespace AttendanceShiftingManagement.Tests;

public sealed class BeneficiaryVerificationPageBindingTests
{
    [Fact]
    public void BeneficiaryVerificationPage_BindsDigitalIdPreviewAndScannerControls()
    {
        var pagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "BeneficiaryVerificationPage.xaml"));

        var xaml = File.ReadAllText(pagePath);

        Assert.Contains("Text=\"Digital ID\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Source=\"{Binding DigitalIdPhotoImage}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Source=\"{Binding DigitalIdQrImage}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding UploadDigitalIdPhotoCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding PrintDigitalIdCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CreateLookupScannerSessionCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding LookupScannerSessionUrl, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding LookupScannerSessionPin}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding PreviousPageCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding NextPageCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedPageSize}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding PageSummary}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void BeneficiaryVerificationPage_DoesNotRenderApprovalTargetPanel()
    {
        var pagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "BeneficiaryVerificationPage.xaml"));

        var xaml = File.ReadAllText(pagePath);

        Assert.DoesNotContain("Text=\"Approval Target\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemsSource=\"{Binding Households}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ItemsSource=\"{Binding AvailableHouseholdMembers}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Hint=\"Optional: link to an existing household member\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding ApprovalActionLabel}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void BeneficiaryVerificationPage_RendersApproveAndRejectOnlyInEditableDetailsActions()
    {
        var pagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Views",
            "BeneficiaryVerificationPage.xaml"));

        var xaml = File.ReadAllText(pagePath);

        Assert.Contains("Content=\"APPROVE\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"REJECT\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Hint=\"Review notes (required for reject)\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"SAVE CORRECTIONS\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"MARK VERIFIED\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"MARK DUPLICATE\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"MARK INACTIVE\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Hint=\"Review notes (required for duplicate, inactive, and reject)\"", xaml, StringComparison.Ordinal);
    }
}
