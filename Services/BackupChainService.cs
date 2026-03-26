using AttendanceShiftingManagement.Models;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace AttendanceShiftingManagement.Services
{
    public static class BackupChainService
    {
        public static BackupPresetStatus BuildPresetStatus(string presetKey, IReadOnlyCollection<BackupManifest> manifests)
        {
            var presetManifests = manifests
                .Where(manifest => string.Equals(manifest.SelectedPreset, presetKey, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return new BackupPresetStatus
            {
                PresetKey = presetKey,
                LastFullBackupAt = presetManifests
                    .Where(manifest => IsType(manifest.BackupType, BackupTypes.Full))
                    .OrderByDescending(manifest => manifest.CreatedAt)
                    .Select(manifest => (DateTime?)manifest.CreatedAt)
                    .FirstOrDefault(),
                LastIncrementalBackupAt = presetManifests
                    .Where(manifest => IsType(manifest.BackupType, BackupTypes.Incremental))
                    .OrderByDescending(manifest => manifest.CreatedAt)
                    .Select(manifest => (DateTime?)manifest.CreatedAt)
                    .FirstOrDefault(),
                LastDifferentialBackupAt = presetManifests
                    .Where(manifest => IsType(manifest.BackupType, BackupTypes.Differential))
                    .OrderByDescending(manifest => manifest.CreatedAt)
                    .Select(manifest => (DateTime?)manifest.CreatedAt)
                    .FirstOrDefault(),
                CanCreateDeltaBackups = presetManifests.Any(manifest => IsType(manifest.BackupType, BackupTypes.Full))
            };
        }

        public static IReadOnlyList<BackupManifest> BuildRestorePlan(BackupManifest target, IReadOnlyCollection<BackupManifest> availableManifests)
        {
            var byId = availableManifests
                .Where(manifest => !string.IsNullOrWhiteSpace(manifest.BackupId))
                .GroupBy(manifest => manifest.BackupId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderByDescending(manifest => manifest.CreatedAt).First(),
                    StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(target.BackupId) && !byId.ContainsKey(target.BackupId))
            {
                byId[target.BackupId] = target;
            }

            if (IsType(target.BackupType, BackupTypes.Full))
            {
                return new[] { RequireManifest(byId, target.BackupId) };
            }

            if (IsType(target.BackupType, BackupTypes.Differential))
            {
                var baseFull = RequireManifest(byId, target.BaseFullBackupId);
                return new[] { baseFull, RequireManifest(byId, target.BackupId) };
            }

            if (!IsType(target.BackupType, BackupTypes.Incremental))
            {
                throw new InvalidOperationException($"Unsupported backup type `{target.BackupType}`.");
            }

            var chain = new List<BackupManifest>();
            var current = RequireManifest(byId, target.BackupId);

            while (true)
            {
                chain.Add(current);
                if (IsType(current.BackupType, BackupTypes.Full))
                {
                    break;
                }

                if (IsType(current.BackupType, BackupTypes.Differential))
                {
                    throw new InvalidOperationException("Incremental restore chains cannot include differential backups.");
                }

                if (string.IsNullOrWhiteSpace(current.PreviousBackupId))
                {
                    throw new InvalidOperationException($"Backup `{current.BackupId}` is missing its previous backup reference.");
                }

                current = RequireManifest(byId, current.PreviousBackupId);
            }

            chain.Reverse();
            return chain;
        }

        public static BackupTableSnapshot BuildDeltaSnapshot(BackupTableSnapshot current, BackupTableSnapshot? baseline)
        {
            var delta = CloneSnapshotMetadata(current);
            delta.Rows.Clear();
            delta.HasChanges = false;

            if (baseline == null)
            {
                delta.SnapshotMode = BackupSnapshotModes.ReplaceTable;
                delta.Rows.AddRange(CloneRows(current.Rows));
                delta.HasChanges = delta.Rows.Count > 0;
                return delta;
            }

            var primaryKeyColumns = current.PrimaryKeyColumns
                .Where(column => !string.IsNullOrWhiteSpace(column))
                .ToList();

            if (primaryKeyColumns.Count == 0)
            {
                var currentFingerprint = BuildTableFingerprint(current);
                var baselineFingerprint = BuildTableFingerprint(baseline);
                delta.SnapshotMode = BackupSnapshotModes.ReplaceTable;
                delta.HasChanges = !string.Equals(currentFingerprint, baselineFingerprint, StringComparison.Ordinal);
                if (delta.HasChanges)
                {
                    delta.Rows.AddRange(CloneRows(current.Rows));
                }

                return delta;
            }

            delta.SnapshotMode = BackupSnapshotModes.UpsertRows;

            var baselineRowsByKey = baseline.Rows.ToDictionary(
                row => BuildRowKey(row, primaryKeyColumns),
                row => row,
                StringComparer.Ordinal);

            foreach (var currentRow in current.Rows)
            {
                var rowKey = BuildRowKey(currentRow, primaryKeyColumns);
                if (!baselineRowsByKey.TryGetValue(rowKey, out var baselineRow)
                    || !string.Equals(BuildRowFingerprint(currentRow, current.Columns), BuildRowFingerprint(baselineRow, baseline.Columns), StringComparison.Ordinal))
                {
                    delta.Rows.Add(CloneRow(currentRow));
                }
            }

            delta.HasChanges = delta.Rows.Count > 0;
            return delta;
        }

        public static BackupManifest BuildNextManifest(
            string presetKey,
            string presetDisplayName,
            string database,
            string backupType,
            IReadOnlyCollection<BackupManifest> manifests,
            DateTime? createdAt = null)
        {
            var normalizedType = NormalizeBackupType(backupType);
            var timestamp = createdAt ?? DateTime.Now;
            var backupId = Guid.NewGuid().ToString("N");
            var latestFull = manifests
                .Where(manifest =>
                    string.Equals(manifest.SelectedPreset, presetKey, StringComparison.OrdinalIgnoreCase)
                    && IsType(manifest.BackupType, BackupTypes.Full))
                .OrderByDescending(manifest => manifest.CreatedAt)
                .FirstOrDefault();

            var manifest = new BackupManifest
            {
                BackupId = backupId,
                BackupType = normalizedType,
                AppVersion = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, "AttendanceShiftingManagement", StringComparison.Ordinal))
                    ?.GetName().Version?.ToString() ?? "unknown",
                SelectedPreset = presetKey,
                PresetDisplayName = presetDisplayName,
                Database = database,
                CreatedAt = timestamp,
                Notes = "Ayuda database rows only. Local configuration files and image files are excluded."
            };

            if (IsType(normalizedType, BackupTypes.Full) || latestFull == null)
            {
                manifest.ChainId = backupId;
                manifest.BaseFullBackupId = backupId;
                manifest.PreviousBackupId = string.Empty;
                return manifest;
            }

            manifest.ChainId = string.IsNullOrWhiteSpace(latestFull.ChainId) ? latestFull.BackupId : latestFull.ChainId;
            manifest.BaseFullBackupId = latestFull.BackupId;

            if (IsType(normalizedType, BackupTypes.Differential))
            {
                manifest.PreviousBackupId = string.Empty;
                return manifest;
            }

            var latestIncremental = manifests
                .Where(existing =>
                    string.Equals(existing.SelectedPreset, presetKey, StringComparison.OrdinalIgnoreCase)
                    && IsType(existing.BackupType, BackupTypes.Incremental)
                    && string.Equals(existing.BaseFullBackupId, latestFull.BackupId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(existing => existing.CreatedAt)
                .FirstOrDefault();

            manifest.PreviousBackupId = latestIncremental?.BackupId ?? latestFull.BackupId;
            return manifest;
        }

        public static string NormalizeBackupType(string backupType)
        {
            if (IsType(backupType, BackupTypes.Incremental))
            {
                return BackupTypes.Incremental;
            }

            if (IsType(backupType, BackupTypes.Differential))
            {
                return BackupTypes.Differential;
            }

            return BackupTypes.Full;
        }

        public static string NormalizeManifestBackupId(string? backupId, string fallbackSeed)
        {
            if (!string.IsNullOrWhiteSpace(backupId))
            {
                return backupId;
            }

            return fallbackSeed.Replace(Path.DirectorySeparatorChar, '_')
                .Replace(Path.AltDirectorySeparatorChar, '_');
        }

        public static BackupTableSnapshot CloneSnapshot(BackupTableSnapshot snapshot)
        {
            var clone = CloneSnapshotMetadata(snapshot);
            clone.Rows.AddRange(CloneRows(snapshot.Rows));
            clone.HasChanges = snapshot.HasChanges;
            return clone;
        }

        public static Dictionary<string, BackupTableSnapshot> ComposeSnapshots(IEnumerable<BackupTableSnapshot> snapshots)
        {
            var state = new Dictionary<string, BackupTableSnapshot>(StringComparer.OrdinalIgnoreCase);

            foreach (var snapshot in snapshots)
            {
                if (!snapshot.HasChanges && snapshot.Rows.Count == 0)
                {
                    continue;
                }

                if (IsReplaceMode(snapshot))
                {
                    state[snapshot.TableName] = CloneSnapshot(snapshot);
                    continue;
                }

                if (!state.TryGetValue(snapshot.TableName, out var currentState))
                {
                    currentState = CloneSnapshotMetadata(snapshot);
                    state[snapshot.TableName] = currentState;
                }

                ApplyUpsertRows(currentState, snapshot);
            }

            return state;
        }

        private static void ApplyUpsertRows(BackupTableSnapshot target, BackupTableSnapshot delta)
        {
            var primaryKeys = target.PrimaryKeyColumns.Count > 0 ? target.PrimaryKeyColumns : delta.PrimaryKeyColumns;
            target.PrimaryKeyColumns = primaryKeys.ToList();
            target.Columns = delta.Columns.Select(column => new BackupColumnMetadata
            {
                Name = column.Name,
                DataType = column.DataType,
                ColumnType = column.ColumnType
            }).ToList();
            target.SnapshotMode = BackupSnapshotModes.ReplaceTable;
            target.HasChanges = true;

            if (primaryKeys.Count == 0)
            {
                target.Rows = CloneRows(delta.Rows).ToList();
                return;
            }

            var rowsByKey = target.Rows.ToDictionary(
                row => BuildRowKey(row, primaryKeys),
                row => row,
                StringComparer.Ordinal);

            foreach (var row in delta.Rows)
            {
                rowsByKey[BuildRowKey(row, primaryKeys)] = CloneRow(row);
            }

            target.Rows = rowsByKey.Values.ToList();
        }

        private static BackupManifest RequireManifest(IReadOnlyDictionary<string, BackupManifest> manifestsById, string? backupId)
        {
            if (string.IsNullOrWhiteSpace(backupId) || !manifestsById.TryGetValue(backupId, out var manifest))
            {
                throw new InvalidOperationException($"Backup chain is missing required backup `{backupId}`.");
            }

            return manifest;
        }

        private static bool IsType(string? candidate, string expected)
        {
            return string.Equals(candidate, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsReplaceMode(BackupTableSnapshot snapshot)
        {
            return string.Equals(snapshot.SnapshotMode, BackupSnapshotModes.ReplaceTable, StringComparison.OrdinalIgnoreCase)
                || snapshot.PrimaryKeyColumns.Count == 0;
        }

        private static BackupTableSnapshot CloneSnapshotMetadata(BackupTableSnapshot snapshot)
        {
            return new BackupTableSnapshot
            {
                TableName = snapshot.TableName,
                SnapshotMode = snapshot.SnapshotMode,
                Columns = snapshot.Columns.Select(column => new BackupColumnMetadata
                {
                    Name = column.Name,
                    DataType = column.DataType,
                    ColumnType = column.ColumnType
                }).ToList(),
                PrimaryKeyColumns = snapshot.PrimaryKeyColumns.ToList(),
                HasChanges = snapshot.HasChanges
            };
        }

        private static IReadOnlyList<Dictionary<string, object?>> CloneRows(IEnumerable<Dictionary<string, object?>> rows)
        {
            return rows.Select(CloneRow).ToList();
        }

        private static Dictionary<string, object?> CloneRow(Dictionary<string, object?> row)
        {
            return row.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        }

        private static string BuildTableFingerprint(BackupTableSnapshot snapshot)
        {
            var orderedRows = snapshot.Rows
                .Select(row => BuildRowFingerprint(row, snapshot.Columns))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            return string.Join("|", orderedRows);
        }

        private static string BuildRowKey(Dictionary<string, object?> row, IReadOnlyCollection<string> primaryKeys)
        {
            return string.Join(
                "|",
                primaryKeys.Select(column => $"{column}={SerializeValue(row.TryGetValue(column, out var value) ? value : null)}"));
        }

        private static string BuildRowFingerprint(Dictionary<string, object?> row, IReadOnlyCollection<BackupColumnMetadata> columns)
        {
            return string.Join(
                "|",
                columns.Select(column => $"{column.Name}={SerializeValue(row.TryGetValue(column.Name, out var value) ? value : null)}"));
        }

        private static string SerializeValue(object? value)
        {
            return value switch
            {
                null => "<null>",
                JsonElement element => element.GetRawText(),
                byte[] bytes => Convert.ToBase64String(bytes),
                DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
                DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
                bool booleanValue => booleanValue ? "true" : "false",
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
                _ => JsonSerializer.Serialize(value)
            };
        }
    }
}
