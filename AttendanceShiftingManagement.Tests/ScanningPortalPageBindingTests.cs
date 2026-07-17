namespace AttendanceShiftingManagement.Tests;

/// <summary>
/// Text-scan binding tests ensuring the Scanning Portal e-Kard validity badge
/// bindings stay wired to their ViewModel members.
/// </summary>
public sealed class ScanningPortalPageBindingTests
{
    private static string ReadSource(params string[] relativeParts)
    {
        var parts = new List<string> { AppContext.BaseDirectory, "..", "..", "..", ".." };
        parts.AddRange(relativeParts);
        return File.ReadAllText(Path.GetFullPath(Path.Combine(parts.ToArray())));
    }

    [Fact]
    public void ScanningPortalPage_BindsEKardValidityBadge()
    {
        var xaml = ReadSource("Views", "ScanningPortalPage.xaml");

        Assert.Contains("{Binding ValidityBadgeText}", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding ValidityBadgeBrush}", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding EKardDetailLine}", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding IsEKardResult", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ScanningPortalViewModel_ExposesEKardBadgeMembers()
    {
        var source = ReadSource("ViewModels", "ScanningPortalViewModel.cs");

        Assert.Contains("public string ValidityBadgeText", source, StringComparison.Ordinal);
        Assert.Contains("public Brush ValidityBadgeBrush", source, StringComparison.Ordinal);
        Assert.Contains("public string EKardDetailLine", source, StringComparison.Ordinal);
        Assert.Contains("public bool IsEKardResult", source, StringComparison.Ordinal);
        // Theme-locked badge colors:
        Assert.Contains("#15803D", source, StringComparison.Ordinal); // VALID
        Assert.Contains("#BE123C", source, StringComparison.Ordinal); // REVOKED
        Assert.Contains("#854D0E", source, StringComparison.Ordinal); // EXPIRED
    }
}
