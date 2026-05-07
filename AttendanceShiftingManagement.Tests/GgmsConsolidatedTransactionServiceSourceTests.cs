namespace AttendanceShiftingManagement.Tests;

public sealed class GgmsConsolidatedTransactionServiceSourceTests
{
    [Fact]
    public void GgmsConsolidatedTransactionService_WritesProjectNameColumn_AndUsesAmsCodesWithFixedFeatureNames()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Services",
            "GgmsConsolidatedTransactionService.cs"));

        var source = File.ReadAllText(sourcePath);

        Assert.Contains("project_name", source, StringComparison.Ordinal);
        Assert.Contains("BuildInsertCommandText(bool includeProjectNameColumn)", source, StringComparison.Ordinal);
        Assert.Contains("HasProjectNameColumnAsync", source, StringComparison.Ordinal);
        Assert.Contains("AidRequestProjectName = \"Aid Request\"", source, StringComparison.Ordinal);
        Assert.Contains("ProjectDistributionProjectName = \"Project Distribution\"", source, StringComparison.Ordinal);
        Assert.Contains("CashForWorkProjectName = \"Cash For Work\"", source, StringComparison.Ordinal);
        Assert.Contains("NormalizeAndLimit($\"AMS-{assistanceCase.CaseNumber}\"", source, StringComparison.Ordinal);
        Assert.Contains("NormalizeAndLimit($\"AMS-PD-{claim.Id:D6}\"", source, StringComparison.Ordinal);
        Assert.Contains("NormalizeAndLimit($\"AMS-{cashForWorkEvent.Id:D6}-{participant.Id:D6}\"", source, StringComparison.Ordinal);
        Assert.Contains("project_name, office_id", source, StringComparison.Ordinal);
        Assert.Contains("OfficeName = \"eKalinga+\"", source, StringComparison.Ordinal);
    }
}
