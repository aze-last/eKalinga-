namespace AttendanceShiftingManagement.Tests;

public sealed class RemotePhaseOneSyncServiceSourceTests
{
    [Fact]
    public void RemotePhaseOneSyncService_TracksPhaseOneTables_AndSupportingReferences()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Services",
            "RemotePhaseOneSyncService.cs"));

        var source = File.ReadAllText(sourcePath);

        Assert.Contains("\"assistance_cases\"", source, StringComparison.Ordinal);
        Assert.Contains("\"cash_for_work_events\"", source, StringComparison.Ordinal);
        Assert.Contains("\"ayuda_project_claims\"", source, StringComparison.Ordinal);
        Assert.Contains("\"budget_ledger_entries\"", source, StringComparison.Ordinal);
        Assert.Contains("\"beneficiary_assistance_ledger\"", source, StringComparison.Ordinal);
        Assert.Contains("\"ayuda_programs\"", source, StringComparison.Ordinal);
        Assert.Contains("\"ayuda_project_beneficiaries\"", source, StringComparison.Ordinal);
        Assert.Contains("\"cash_for_work_participants\"", source, StringComparison.Ordinal);
    }
}
