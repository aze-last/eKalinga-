using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using System.Reflection;

namespace AttendanceShiftingManagement.Tests;

public sealed class BackupFeatureTests
{
    [Fact]
    public void BackupManifest_ExposesChainMetadataProperties()
    {
        AssertPropertyExists(nameof(BackupManifest), "BackupType");
        AssertPropertyExists(nameof(BackupManifest), "BackupId");
        AssertPropertyExists(nameof(BackupManifest), "ChainId");
        AssertPropertyExists(nameof(BackupManifest), "BaseFullBackupId");
        AssertPropertyExists(nameof(BackupManifest), "PreviousBackupId");
    }

    [Fact]
    public void BuildRestorePlan_ForIncrementalTarget_ReturnsFullPlusIncrementalChain()
    {
        var full = CreateManifest("full-001", "Full", "chain-a", null, null, new DateTime(2026, 3, 25, 8, 0, 0));
        var incrementOne = CreateManifest("inc-001", "Incremental", "chain-a", "full-001", "full-001", new DateTime(2026, 3, 25, 9, 0, 0));
        var incrementTwo = CreateManifest("inc-002", "Incremental", "chain-a", "full-001", "inc-001", new DateTime(2026, 3, 25, 10, 0, 0));

        var plan = InvokeBuildRestorePlan(incrementTwo, new[] { incrementTwo, full, incrementOne });
        var orderedIds = plan.Select(ReadStringProperty).ToArray();

        Assert.Equal(new[] { "full-001", "inc-001", "inc-002" }, orderedIds);
    }

    [Fact]
    public void BuildDeltaSnapshot_WithPrimaryKeys_KeepsOnlyNewAndChangedRows()
    {
        var baseline = new BackupTableSnapshot
        {
            TableName = "Household",
            Columns =
            {
                new BackupColumnMetadata { Name = "Id", DataType = "int", ColumnType = "int" },
                new BackupColumnMetadata { Name = "Name", DataType = "varchar", ColumnType = "varchar(100)" }
            },
            Rows =
            {
                new Dictionary<string, object?> { ["Id"] = 1, ["Name"] = "Alpha" },
                new Dictionary<string, object?> { ["Id"] = 2, ["Name"] = "Bravo" }
            }
        };

        var current = new BackupTableSnapshot
        {
            TableName = "Household",
            Columns =
            {
                new BackupColumnMetadata { Name = "Id", DataType = "int", ColumnType = "int" },
                new BackupColumnMetadata { Name = "Name", DataType = "varchar", ColumnType = "varchar(100)" }
            },
            Rows =
            {
                new Dictionary<string, object?> { ["Id"] = 1, ["Name"] = "Alpha" },
                new Dictionary<string, object?> { ["Id"] = 2, ["Name"] = "Bravo Updated" },
                new Dictionary<string, object?> { ["Id"] = 3, ["Name"] = "Charlie" }
            }
        };

        SetProperty(baseline, "PrimaryKeyColumns", new List<string> { "Id" });
        SetProperty(current, "PrimaryKeyColumns", new List<string> { "Id" });

        var delta = InvokeBuildDeltaSnapshot(current, baseline);

        Assert.Equal("UpsertRows", ReadStringProperty(delta, "SnapshotMode"));

        var rows = ReadRows(delta);
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, row => Equals(row["Id"], 2) && Equals(row["Name"], "Bravo Updated"));
        Assert.Contains(rows, row => Equals(row["Id"], 3) && Equals(row["Name"], "Charlie"));
    }

    [Fact]
    public void BuildDeltaSnapshot_WithoutPrimaryKeys_FallsBackToReplaceTable()
    {
        var baseline = new BackupTableSnapshot
        {
            TableName = "AuditTrail",
            Columns =
            {
                new BackupColumnMetadata { Name = "Description", DataType = "varchar", ColumnType = "varchar(255)" }
            },
            Rows =
            {
                new Dictionary<string, object?> { ["Description"] = "Old Row" }
            }
        };

        var current = new BackupTableSnapshot
        {
            TableName = "AuditTrail",
            Columns =
            {
                new BackupColumnMetadata { Name = "Description", DataType = "varchar", ColumnType = "varchar(255)" }
            },
            Rows =
            {
                new Dictionary<string, object?> { ["Description"] = "Current Row" }
            }
        };

        SetProperty(baseline, "PrimaryKeyColumns", new List<string>());
        SetProperty(current, "PrimaryKeyColumns", new List<string>());

        var delta = InvokeBuildDeltaSnapshot(current, baseline);

        Assert.Equal("ReplaceTable", ReadStringProperty(delta, "SnapshotMode"));
        var rows = ReadRows(delta);
        Assert.Single(rows);
        Assert.Equal("Current Row", rows[0]["Description"]);
    }

    [Fact]
    public void BuildPresetStatus_PicksLatestBackupPerTypeForSelectedPreset()
    {
        var manifests = new[]
        {
            CreateManifest("full-old", "Full", "chain-old", null, null, new DateTime(2026, 3, 24, 8, 0, 0)),
            CreateManifest("full-new", "Full", "chain-new", null, null, new DateTime(2026, 3, 25, 8, 0, 0)),
            CreateManifest("inc-new", "Incremental", "chain-new", "full-new", "full-new", new DateTime(2026, 3, 25, 9, 0, 0)),
            CreateManifest("diff-new", "Differential", "chain-new", "full-new", null, new DateTime(2026, 3, 25, 10, 0, 0))
        };

        var status = InvokeBuildPresetStatus("Local", manifests);

        Assert.True(ReadBooleanProperty(status, "CanCreateDeltaBackups"));
        Assert.Equal(new DateTime(2026, 3, 25, 8, 0, 0), ReadNullableDateTimeProperty(status, "LastFullBackupAt"));
        Assert.Equal(new DateTime(2026, 3, 25, 9, 0, 0), ReadNullableDateTimeProperty(status, "LastIncrementalBackupAt"));
        Assert.Equal(new DateTime(2026, 3, 25, 10, 0, 0), ReadNullableDateTimeProperty(status, "LastDifferentialBackupAt"));
    }

    [Fact]
    public void ResolveTargetTableName_PreservesActualTargetCase()
    {
        var resolved = LocalBackupService.ResolveTargetTableName(
            ["users", "BeneficiaryStaging", "cash_for_work_events"],
            "beneficiarystaging");

        Assert.Equal("BeneficiaryStaging", resolved);
    }

    [Fact]
    public void ResolveTargetTableName_ReturnsNullWhenTableIsMissing()
    {
        var resolved = LocalBackupService.ResolveTargetTableName(
            ["users", "cash_for_work_events"],
            "BeneficiaryStaging");

        Assert.Null(resolved);
    }

    private static void AssertPropertyExists(string typeName, string propertyName)
    {
        var property = typeof(BackupManifest).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.True(property != null, $"{typeName} should expose a public `{propertyName}` property.");
    }

    private static BackupManifest CreateManifest(
        string backupId,
        string backupType,
        string chainId,
        string? baseFullBackupId,
        string? previousBackupId,
        DateTime createdAt)
    {
        var manifest = new BackupManifest
        {
            SelectedPreset = "Local",
            Database = "attendance_shifting_db",
            CreatedAt = createdAt
        };

        SetProperty(manifest, "BackupId", backupId);
        SetProperty(manifest, "BackupType", backupType);
        SetProperty(manifest, "ChainId", chainId);
        SetProperty(manifest, "BaseFullBackupId", baseFullBackupId);
        SetProperty(manifest, "PreviousBackupId", previousBackupId);
        return manifest;
    }

    private static IReadOnlyList<BackupManifest> InvokeBuildRestorePlan(BackupManifest target, IReadOnlyCollection<BackupManifest> available)
    {
        var serviceType = typeof(BackupManifest).Assembly.GetType("AttendanceShiftingManagement.Services.BackupChainService");
        Assert.True(serviceType != null, "BackupChainService type should exist.");

        var method = serviceType!.GetMethod("BuildRestorePlan", BindingFlags.Public | BindingFlags.Static);
        Assert.True(method != null, "BackupChainService.BuildRestorePlan should exist.");

        var result = method!.Invoke(null, new object[] { target, available });
        Assert.NotNull(result);
        return Assert.IsAssignableFrom<IReadOnlyList<BackupManifest>>(result);
    }

    private static BackupTableSnapshot InvokeBuildDeltaSnapshot(BackupTableSnapshot current, BackupTableSnapshot baseline)
    {
        var serviceType = typeof(BackupManifest).Assembly.GetType("AttendanceShiftingManagement.Services.BackupChainService");
        Assert.True(serviceType != null, "BackupChainService type should exist.");

        var method = serviceType!.GetMethod("BuildDeltaSnapshot", BindingFlags.Public | BindingFlags.Static);
        Assert.True(method != null, "BackupChainService.BuildDeltaSnapshot should exist.");

        var result = method!.Invoke(null, new object[] { current, baseline });
        Assert.NotNull(result);
        return Assert.IsType<BackupTableSnapshot>(result);
    }

    private static object InvokeBuildPresetStatus(string presetKey, IReadOnlyCollection<BackupManifest> manifests)
    {
        var serviceType = typeof(BackupManifest).Assembly.GetType("AttendanceShiftingManagement.Services.BackupChainService");
        Assert.True(serviceType != null, "BackupChainService type should exist.");

        var method = serviceType!.GetMethod("BuildPresetStatus", BindingFlags.Public | BindingFlags.Static);
        Assert.True(method != null, "BackupChainService.BuildPresetStatus should exist.");

        var result = method!.Invoke(null, new object[] { presetKey, manifests });
        Assert.NotNull(result);
        return result!;
    }

    private static void SetProperty(object target, string propertyName, object? value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.True(property != null, $"{target.GetType().Name} should expose a public `{propertyName}` property.");
        property!.SetValue(target, value);
    }

    private static string ReadStringProperty(object target)
    {
        return ReadStringProperty(target, "BackupId");
    }

    private static string ReadStringProperty(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.True(property != null, $"{target.GetType().Name} should expose a public `{propertyName}` property.");
        return Assert.IsType<string>(property!.GetValue(target));
    }

    private static bool ReadBooleanProperty(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.True(property != null, $"{target.GetType().Name} should expose a public `{propertyName}` property.");
        return Assert.IsType<bool>(property!.GetValue(target));
    }

    private static DateTime? ReadNullableDateTimeProperty(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.True(property != null, $"{target.GetType().Name} should expose a public `{propertyName}` property.");
        return (DateTime?)property!.GetValue(target);
    }

    private static IReadOnlyList<Dictionary<string, object?>> ReadRows(BackupTableSnapshot snapshot)
    {
        return snapshot.Rows;
    }
}
