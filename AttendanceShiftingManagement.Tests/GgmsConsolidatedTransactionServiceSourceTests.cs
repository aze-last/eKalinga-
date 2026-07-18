using AttendanceShiftingManagement.Services;

namespace AttendanceShiftingManagement.Tests;

public sealed class GgmsConsolidatedTransactionServiceSourceTests
{
    private static string ReadServiceSource()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Services",
            "GgmsConsolidatedTransactionService.cs"));

        return File.ReadAllText(sourcePath);
    }

    [Fact]
    public void GgmsConsolidatedTransactionService_WritesProjectNameColumn_AndUsesFixedFeatureNames()
    {
        var source = ReadServiceSource();

        Assert.Contains("project_name", source, StringComparison.Ordinal);
        Assert.Contains("BuildInsertCommandText(IReadOnlySet<string> optionalColumns)", source, StringComparison.Ordinal);
        Assert.Contains("GetOptionalColumnsAsync", source, StringComparison.Ordinal);
        Assert.Contains("AidRequestProjectName = \"Aid Request\"", source, StringComparison.Ordinal);
        Assert.Contains("ProjectDistributionProjectName = \"Project Distribution\"", source, StringComparison.Ordinal);
        Assert.Contains("CashForWorkProjectName = \"Cash For Work\"", source, StringComparison.Ordinal);
        Assert.Contains("OfficeName = \"eKalinga+\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GgmsConsolidatedTransactionService_UsesTwoTierProjectIdentity()
    {
        var source = ReadServiceSource();

        // project_code is always the stable AMS project code — never the GGMS OPP- id.
        Assert.Contains("BuildProjectCode(AyudaProgram? program, string fallbackReference)", source, StringComparison.Ordinal);
        Assert.Contains("$\"AMS-{program.Id:D6}\"", source, StringComparison.Ordinal);
        Assert.Contains("BuildProjectCode(program, $\"AMS-PD-{claim.Id:D6}\")", source, StringComparison.Ordinal);
        Assert.Contains("BuildProjectCode(program, $\"AMS-{assistanceCase.CaseNumber}\")", source, StringComparison.Ordinal);
        Assert.Contains("BuildProjectCode(cfwProgram, $\"AMS-CFW-{cashForWorkEvent.Id:D6}\")", source, StringComparison.Ordinal);

        // project_details_id carries the GGMS mapping (nullable — NULL when unmapped is correct).
        Assert.Contains("project_details_id", source, StringComparison.Ordinal);
        Assert.Contains("NormalizeAndLimit(program?.SourceProjectDetailsId, SharedColumnMaxLength)", source, StringComparison.Ordinal);

        // The GGMS id must never be written into project_code (the pre-v2 bug).
        Assert.DoesNotContain(
            "var projectCode = NormalizeAndLimit(program?.SourceProjectDetailsId",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GgmsConsolidatedTransactionService_DerivesBarangayAndHouseholdNoLocally()
    {
        var source = ReadServiceSource();

        Assert.Contains("GetHouseholdNo(string? beneficiaryId)", source, StringComparison.Ordinal);
        Assert.Contains("ParseBarangayFromAddress(string? address)", source, StringComparison.Ordinal);
        Assert.Contains("barangay", source, StringComparison.Ordinal);
        Assert.Contains("household_no", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GetHouseholdNo_ParsesThirdSegment_OnlyForCrsFormatIds()
    {
        Assert.Equal("692811519", GgmsConsolidatedTransactionService.GetHouseholdNo("BEN-2026-692811519-1"));
        Assert.Null(GgmsConsolidatedTransactionService.GetHouseholdNo("AMS-PD-000048"));
        Assert.Null(GgmsConsolidatedTransactionService.GetHouseholdNo("E-1001"));
        Assert.Null(GgmsConsolidatedTransactionService.GetHouseholdNo(null));
        Assert.Null(GgmsConsolidatedTransactionService.GetHouseholdNo(""));
    }

    [Fact]
    public void ParseBarangayFromAddress_TakesSecondCommaSegment()
    {
        Assert.Equal(
            "Balasinon",
            GgmsConsolidatedTransactionService.ParseBarangayFromAddress("Purok-06, Balasinon, Sulop, Davao del Sur"));
        Assert.Null(GgmsConsolidatedTransactionService.ParseBarangayFromAddress("Sulop"));
        Assert.Null(GgmsConsolidatedTransactionService.ParseBarangayFromAddress(null));
    }
}
