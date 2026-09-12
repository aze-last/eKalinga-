using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class WhatsNewServiceTests
{
    [Fact]
    public void ShouldShowWhatsNew_WithoutLastSeenVersion_ShowsAllEntriesUpToCurrent()
    {
        var shown = WhatsNewService.ShouldShowWhatsNew("1.0.9", string.Empty, out var entries);

        Assert.True(shown);
        Assert.Equal(2, entries.Count);
        Assert.Equal("1.0.9", entries[0].Version);
        Assert.Equal("1.0.8", entries[1].Version);
    }

    [Fact]
    public void ShouldShowWhatsNew_WithOlderLastSeenVersion_ShowsOnlyNewerEntries()
    {
        var shown = WhatsNewService.ShouldShowWhatsNew("1.0.9", "1.0.8", out var entries);

        Assert.True(shown);
        var single = Assert.Single(entries);
        Assert.Equal("1.0.9", single.Version);
    }

    [Fact]
    public void ShouldShowWhatsNew_WithLatestLastSeenVersion_ShowsOnlyNewestEntry()
    {
        var shown = WhatsNewService.ShouldShowWhatsNew("1.0.10", "1.0.9", out var entries);

        Assert.True(shown);
        var single = Assert.Single(entries);
        Assert.Equal("1.0.10", single.Version);
    }

    [Fact]
    public void ShouldShowWhatsNew_AfterPatchIncrement_ShowsOnlyNewestEntry()
    {
        var shown = WhatsNewService.ShouldShowWhatsNew("1.0.11", "1.0.10", out var entries);

        Assert.True(shown);
        var single = Assert.Single(entries);
        Assert.Equal("1.0.11", single.Version);
    }

    [Fact]
    public void ShouldShowWhatsNew_AfterLatestPatchIncrement_ShowsOnlyNewestEntry()
    {
        var shown = WhatsNewService.ShouldShowWhatsNew("1.0.12", "1.0.11", out var entries);

        Assert.True(shown);
        var single = Assert.Single(entries);
        Assert.Equal("1.0.12", single.Version);
    }

    [Fact]
    public void ShouldShowWhatsNew_WhenAlreadySeen_DoesNotShow()
    {
        var shown = WhatsNewService.ShouldShowWhatsNew("1.0.9", "1.0.9", out var entries);

        Assert.False(shown);
        Assert.Empty(entries);
    }

    [Fact]
    public void ShouldShowWhatsNew_WithOlderCurrentVersion_ShowsOnlyApplicableEntries()
    {
        var shown = WhatsNewService.ShouldShowWhatsNew("1.0.8", string.Empty, out var entries);

        Assert.True(shown);
        var single = Assert.Single(entries);
        Assert.Equal("1.0.8", single.Version);
    }

    [Fact]
    public void ShouldShowWhatsNew_WithInvalidCurrentVersion_DoesNotShow()
    {
        var shown = WhatsNewService.ShouldShowWhatsNew("not-a-version", string.Empty, out var entries);

        Assert.False(shown);
        Assert.Empty(entries);
    }
}
