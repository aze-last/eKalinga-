using System;
using System.Collections.Generic;

namespace AttendanceShiftingManagement.Models
{
    public sealed class BackupManifest
    {
        public string BackupFormatVersion { get; set; } = "1.0";
        public string AppVersion { get; set; } = string.Empty;
        public string SelectedPreset { get; set; } = string.Empty;
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
        public List<BackupColumnMetadata> Columns { get; set; } = new();
        public List<Dictionary<string, object?>> Rows { get; set; } = new();
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
}
