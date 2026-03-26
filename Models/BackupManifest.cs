using System;
using System.Collections.Generic;

namespace AttendanceShiftingManagement.Models
{
    public static class BackupTypes
    {
        public const string Full = "Full";
        public const string Incremental = "Incremental";
        public const string Differential = "Differential";
    }

    public static class BackupSnapshotModes
    {
        public const string ReplaceTable = "ReplaceTable";
        public const string UpsertRows = "UpsertRows";
    }

    public sealed class BackupManifest
    {
        public string BackupFormatVersion { get; set; } = "2.0";
        public string BackupId { get; set; } = string.Empty;
        public string BackupType { get; set; } = BackupTypes.Full;
        public string ChainId { get; set; } = string.Empty;
        public string BaseFullBackupId { get; set; } = string.Empty;
        public string PreviousBackupId { get; set; } = string.Empty;
        public string AppVersion { get; set; } = string.Empty;
        public string SelectedPreset { get; set; } = string.Empty;
        public string PresetDisplayName { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public List<string> IncludedTables { get; set; } = new();
        public List<string> ExcludedTables { get; set; } = new();
        public Dictionary<string, int> RowCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public int TotalRows { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    public sealed class BackupTableSnapshot
    {
        public string TableName { get; set; } = string.Empty;
        public string SnapshotMode { get; set; } = BackupSnapshotModes.ReplaceTable;
        public List<BackupColumnMetadata> Columns { get; set; } = new();
        public List<string> PrimaryKeyColumns { get; set; } = new();
        public List<Dictionary<string, object?>> Rows { get; set; } = new();
        public bool HasChanges { get; set; } = true;
    }

    public sealed class BackupColumnMetadata
    {
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public string ColumnType { get; set; } = string.Empty;
    }

    public sealed class BackupOperationResult
    {
        public bool IsSuccess { get; init; }
        public string Message { get; init; } = string.Empty;
        public string? FilePath { get; init; }
        public BackupManifest? Manifest { get; init; }
        public List<string> Warnings { get; init; } = new();
    }

    public sealed class BackupCatalogEntry
    {
        public string BackupId { get; set; } = string.Empty;
        public string BackupType { get; set; } = BackupTypes.Full;
        public string ChainId { get; set; } = string.Empty;
        public string BaseFullBackupId { get; set; } = string.Empty;
        public string PreviousBackupId { get; set; } = string.Empty;
        public string SelectedPreset { get; set; } = string.Empty;
        public string PresetDisplayName { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string FilePath { get; set; } = string.Empty;
    }

    public sealed class BackupCatalogModel
    {
        public List<BackupCatalogEntry> Entries { get; set; } = new();
    }

    public sealed class BackupPresetStatus
    {
        public string PresetKey { get; set; } = string.Empty;
        public DateTime? LastFullBackupAt { get; set; }
        public DateTime? LastIncrementalBackupAt { get; set; }
        public DateTime? LastDifferentialBackupAt { get; set; }
        public bool CanCreateDeltaBackups { get; set; }
    }
}
