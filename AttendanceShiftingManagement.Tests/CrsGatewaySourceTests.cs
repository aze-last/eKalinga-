namespace AttendanceShiftingManagement.Tests;

/// <summary>
/// Text-scan tests asserting the e-Kard CRS integration follows the verification
/// contract verbatim: most-recent-row status pattern, two-hop raw-SQL photo lookup,
/// append-only record_access_logs audit, GuidFormat=None, and no writes to the
/// read-only CRS tables.
/// </summary>
public sealed class CrsGatewaySourceTests
{
    private static string ReadSource(params string[] relativeParts)
    {
        var parts = new List<string> { AppContext.BaseDirectory, "..", "..", "..", ".." };
        parts.AddRange(relativeParts);
        return File.ReadAllText(Path.GetFullPath(Path.Combine(parts.ToArray())));
    }

    [Fact]
    public void CrsGateway_UsesMostRecentRowStatusPattern_NotStatusActiveFilter()
    {
        var source = ReadSource("Services", "CRS", "CrsGateway.cs");

        Assert.Contains("FROM digital_ids", source, StringComparison.Ordinal);
        Assert.Contains("WHERE beneficiary_id = @beneficiaryId AND IsDeleted = 0", source, StringComparison.Ordinal);
        Assert.Contains("ORDER BY issued_date DESC", source, StringComparison.Ordinal);
        Assert.Contains("id_number, status, issued_date, expiry_date, revoked_at, revocation_reason", source, StringComparison.Ordinal);
        // The contract's forbidden pattern:
        Assert.DoesNotContain("WHERE status = 'Active'", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("status='Active'", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CrsGateway_UsesTwoHopRawSqlPhotoLookup()
    {
        var source = ReadSource("Services", "CRS", "CrsGateway.cs");

        Assert.Contains("SELECT demographic_characteristic_id FROM val_beneficiaries", source, StringComparison.Ordinal);
        Assert.Contains("SELECT profile_picture, updated_at FROM demographic_characteristics", source, StringComparison.Ordinal);
        Assert.Contains("SELECT updated_at FROM demographic_characteristics", source, StringComparison.Ordinal);
        Assert.Contains("MySqlCommand", source, StringComparison.Ordinal);
        // Read-only hard rules — no writes to the CRS-owned tables:
        Assert.DoesNotContain("UPDATE digital_ids", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO digital_ids", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM digital_ids", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE val_beneficiaries", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE demographic_characteristics", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE record_access_logs", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM record_access_logs", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CrsGateway_InsertsAccessLogWithContractColumns()
    {
        var source = ReadSource("Services", "CRS", "CrsGateway.cs");

        Assert.Contains("INSERT INTO record_access_logs", source, StringComparison.Ordinal);
        Assert.Contains("(user_id, user_name, record_type, reference_no, action_taken, accessed_at, SyncId)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CrsVerificationService_UsesContractRecordTypeAndActionFormatAndSyncId()
    {
        var source = ReadSource("Services", "CRS", "CrsDigitalIdVerificationService.cs");

        Assert.Contains("\"DIGITAL_ID_VERIFICATION\"", source, StringComparison.Ordinal);
        Assert.Contains("VERIFY — e-Kard check by {SystemName}: {status}", source, StringComparison.Ordinal);
        Assert.Contains("Guid.NewGuid().ToString()", source, StringComparison.Ordinal);
        Assert.Contains("SystemName = \"eKalinga+\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CrsGateway_TruncatesActionTakenToLiveColumnWidth()
    {
        var source = ReadSource("Services", "CRS", "CrsGateway.cs");

        // record_access_logs.action_taken is varchar(50) on the live CRS DB.
        Assert.Contains("Truncate(entry.ActionTaken, 50)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CrsVerificationService_ChecksExpiryAlongsideStatus_DateOnly()
    {
        var source = ReadSource("Services", "CRS", "CrsDigitalIdVerificationService.cs");

        Assert.Contains("ExpiryDate.Value.Date < DateTime.Today", source, StringComparison.Ordinal);
        Assert.Contains("EKardValidity.Expired", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CrsContractConnection_AppliesGuidFormatNone()
    {
        var provider = ReadSource("Services", "CRS", "CrsContractConnectionProvider.cs");
        Assert.Contains("guidFormatNone: true", provider, StringComparison.Ordinal);

        var settings = ReadSource("Services", "ConnectionSettingsService.cs");
        Assert.Contains("MySqlGuidFormat.None", settings, StringComparison.Ordinal);
    }

    [Fact]
    public void ScanSurfaces_RouteBenPayloadsToEKardFlow()
    {
        var scanningPortal = ReadSource("ViewModels", "ScanningPortalViewModel.cs");
        Assert.Contains("EKardPayloadRouter.IsEKardPayload", scanningPortal, StringComparison.Ordinal);

        var masterList = ReadSource("ViewModels", "MasterListViewModel.cs");
        Assert.Contains("EKardPayloadRouter.IsEKardPayload", masterList, StringComparison.Ordinal);

        var distribution = ReadSource("ViewModels", "ProjectDistributionViewModel.cs");
        Assert.Contains("EKardPayloadRouter.IsEKardPayload", distribution, StringComparison.Ordinal);
    }
}
