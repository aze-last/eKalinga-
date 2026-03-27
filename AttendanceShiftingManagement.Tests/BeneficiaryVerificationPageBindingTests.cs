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
}
