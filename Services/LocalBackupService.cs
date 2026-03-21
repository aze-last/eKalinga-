using AttendanceShiftingManagement.Models;
using MySqlConnector;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;

namespace AttendanceShiftingManagement.Services
{
    public static class LocalBackupService
    {
        private static readonly HashSet<string> ExcludedTables = new(StringComparer.OrdinalIgnoreCase)
        {
            "__EFMigrationsHistory",
            "fingerprint_templates"
        };

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static async Task<BackupOperationResult> CreateBackupAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                var settings = ConnectionSettingsService.Load();
                var preset = settings.GetPreset(settings.SelectedPreset);
                var manifest = new BackupManifest
                {
                    AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
                    SelectedPreset = settings.SelectedPreset,
                    Database = preset.Database,
                    Notes = "Database rows only. Fingerprint templates, local config files, and image files are excluded."
                };

                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await using var connection = new MySqlConnection(ConnectionSettingsService.BuildConnectionString(preset));
                await connection.OpenAsync(cancellationToken);

                var tableNames = await ListBackupTablesAsync(connection, cancellationToken);
                manifest.ExcludedTables = tableNames
                    .Where(table => ExcludedTables.Contains(table))
                    .OrderBy(table => table, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                await using var fileStream = File.Create(filePath);
                using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: false);

                foreach (var tableName in tableNames.Where(table => !ExcludedTables.Contains(table)))
                {
                    var snapshot = await ReadTableSnapshotAsync(connection, tableName, cancellationToken);
                    manifest.IncludedTables.Add(tableName);
                    manifest.RowCounts[tableName] = snapshot.Rows.Count;
                    manifest.TotalRows += snapshot.Rows.Count;

                    var entry = archive.CreateEntry($"tables/{tableName}.json", CompressionLevel.Optimal);
                    await using var entryStream = entry.Open();
                    await JsonSerializer.SerializeAsync(entryStream, snapshot, JsonOptions, cancellationToken);
                }

                var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                await using (var manifestStream = manifestEntry.Open())
                {
                    await JsonSerializer.SerializeAsync(manifestStream, manifest, JsonOptions, cancellationToken);
                }

                return new BackupOperationResult
                {
                    IsSuccess = true,
                    FilePath = filePath,
                    Manifest = manifest,
                    Message = $"Backup created successfully with {manifest.TotalRows} row(s) across {manifest.IncludedTables.Count} table(s)."
                };
            }
            catch (Exception ex)
            {
                return new BackupOperationResult
                {
                    IsSuccess = false,
                    FilePath = filePath,
                    Message = $"Backup failed: {ex.Message}"
                };
            }
        }

        public static async Task<BackupManifest?> ReadManifestAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            await using var fileStream = File.OpenRead(filePath);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: false);
            var manifestEntry = archive.GetEntry("manifest.json");
            if (manifestEntry == null)
            {
                return null;
            }

            await using var manifestStream = manifestEntry.Open();
            return await JsonSerializer.DeserializeAsync<BackupManifest>(manifestStream, JsonOptions, cancellationToken);
        }

        public static async Task<BackupOperationResult> RestoreBackupAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                var manifest = await ReadManifestAsync(filePath, cancellationToken);
                if (manifest == null)
                {
                    return new BackupOperationResult
                    {
                        IsSuccess = false,
                        FilePath = filePath,
                        Message = "Restore failed: manifest.json was not found in the backup archive."
                    };
                }

                var settings = ConnectionSettingsService.Load();
                var preset = settings.GetPreset(settings.SelectedPreset);
                var warnings = new List<string>();

                await using var connection = new MySqlConnection(ConnectionSettingsService.BuildConnectionString(preset));
                await connection.OpenAsync(cancellationToken);

                var currentTables = await ListBackupTablesAsync(connection, cancellationToken);
                var restorableTables = currentTables
                    .Where(table => !ExcludedTables.Contains(table))
                    .ToList();

                await using var fileStream = File.OpenRead(filePath);
                using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: false);

                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                try
                {
                    await ExecuteNonQueryAsync(connection, transaction, "SET FOREIGN_KEY_CHECKS = 0;", cancellationToken);

                    foreach (var tableName in restorableTables)
                    {
                        await ExecuteNonQueryAsync(connection, transaction, $"DELETE FROM {EscapeIdentifier(tableName)};", cancellationToken);
                    }

                    foreach (var tableName in manifest.IncludedTables)
                    {
                        if (!restorableTables.Contains(tableName, StringComparer.OrdinalIgnoreCase))
                        {
                            warnings.Add($"Skipped `{tableName}` because it does not exist in the current database.");
                            continue;
                        }

                        var entry = archive.GetEntry($"tables/{tableName}.json");
                        if (entry == null)
                        {
                            warnings.Add($"Skipped `{tableName}` because its snapshot entry was missing from the archive.");
                            continue;
                        }

                        await using var entryStream = entry.Open();
                        var snapshot = await JsonSerializer.DeserializeAsync<BackupTableSnapshot>(entryStream, JsonOptions, cancellationToken);
                        if (snapshot == null)
                        {
                            warnings.Add($"Skipped `{tableName}` because its snapshot could not be read.");
                            continue;
                        }

                        await InsertSnapshotAsync(connection, transaction, snapshot, cancellationToken);
                    }

                    await ExecuteNonQueryAsync(connection, transaction, "SET FOREIGN_KEY_CHECKS = 1;", cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }

                var warningSuffix = warnings.Count == 0
                    ? string.Empty
                    : $" Warnings: {string.Join(" ", warnings)}";

                return new BackupOperationResult
                {
                    IsSuccess = true,
                    FilePath = filePath,
                    Manifest = manifest,
                    Warnings = warnings,
                    Message = $"Restore completed for {manifest.TotalRows} row(s) into `{preset.Database}`.{warningSuffix}"
                };
            }
            catch (Exception ex)
            {
                return new BackupOperationResult
                {
                    IsSuccess = false,
                    FilePath = filePath,
                    Message = $"Restore failed: {ex.Message}"
                };
            }
        }

        private static async Task<List<string>> ListBackupTablesAsync(MySqlConnection connection, CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT TABLE_NAME
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_TYPE = 'BASE TABLE'
                ORDER BY TABLE_NAME;
                """;

            var tables = new List<string>();
            await using var command = new MySqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                tables.Add(reader.GetString(0));
            }

            return tables;
        }

        private static async Task<BackupTableSnapshot> ReadTableSnapshotAsync(MySqlConnection connection, string tableName, CancellationToken cancellationToken)
        {
            var snapshot = new BackupTableSnapshot
            {
                TableName = tableName,
                Columns = await GetColumnMetadataAsync(connection, tableName, cancellationToken)
            };

            await using var command = new MySqlCommand($"SELECT * FROM {EscapeIdentifier(tableName)};", connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var index = 0; index < reader.FieldCount; index++)
                {
                    if (await reader.IsDBNullAsync(index, cancellationToken))
                    {
                        row[reader.GetName(index)] = null;
                    }
                    else
                    {
                        row[reader.GetName(index)] = NormalizeBackupValue(reader.GetValue(index));
                    }
                }

                snapshot.Rows.Add(row);
            }

            return snapshot;
        }

        private static async Task<List<BackupColumnMetadata>> GetColumnMetadataAsync(MySqlConnection connection, string tableName, CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT COLUMN_NAME, DATA_TYPE, COLUMN_TYPE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = @tableName
                ORDER BY ORDINAL_POSITION;
                """;

            var columns = new List<BackupColumnMetadata>();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@tableName", tableName);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(new BackupColumnMetadata
                {
                    Name = reader.GetString(0),
                    DataType = reader.GetString(1),
                    ColumnType = reader.GetString(2)
                });
            }

            return columns;
        }

        private static async Task InsertSnapshotAsync(
            MySqlConnection connection,
            MySqlTransaction transaction,
            BackupTableSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            if (snapshot.Rows.Count == 0 || snapshot.Columns.Count == 0)
            {
                return;
            }

            var columnList = string.Join(", ", snapshot.Columns.Select(column => EscapeIdentifier(column.Name)));
            var parameterList = string.Join(", ", snapshot.Columns.Select((_, index) => $"@p{index}"));
            var sql = $"INSERT INTO {EscapeIdentifier(snapshot.TableName)} ({columnList}) VALUES ({parameterList});";

            foreach (var row in snapshot.Rows)
            {
                await using var command = new MySqlCommand(sql, connection, transaction);
                for (var index = 0; index < snapshot.Columns.Count; index++)
                {
                    var column = snapshot.Columns[index];
                    row.TryGetValue(column.Name, out var rawValue);
                    command.Parameters.AddWithValue($"@p{index}", ConvertToDatabaseValue(rawValue, column));
                }

                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        private static object ConvertToDatabaseValue(object? rawValue, BackupColumnMetadata column)
        {
            if (rawValue == null)
            {
                return DBNull.Value;
            }

            if (rawValue is JsonElement element)
            {
                return ConvertJsonElementToDatabaseValue(element, column);
            }

            return rawValue;
        }

        private static object ConvertJsonElementToDatabaseValue(JsonElement element, BackupColumnMetadata column)
        {
            if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return DBNull.Value;
            }

            var dataType = column.DataType.ToLowerInvariant();
            return dataType switch
            {
                "bigint" => element.GetInt64(),
                "int" or "integer" or "mediumint" => element.GetInt32(),
                "smallint" => element.GetInt16(),
                "tinyint" => ConvertTinyIntValue(element, column.ColumnType),
                "bit" => element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False
                    ? element.GetBoolean()
                    : element.GetInt32(),
                "decimal" or "numeric" => element.GetDecimal(),
                "double" or "real" => element.GetDouble(),
                "float" => (float)element.GetDouble(),
                "date" or "datetime" or "timestamp" => ParseDateTimeValue(element),
                "time" => TimeSpan.Parse(ReadElementAsString(element) ?? "00:00:00", CultureInfo.InvariantCulture),
                "char" or "varchar" or "text" or "tinytext" or "mediumtext" or "longtext" or "enum" or "set"
                    => ReadElementAsString(element) ?? string.Empty,
                "binary" or "varbinary" or "blob" or "tinyblob" or "mediumblob" or "longblob"
                    => ParseBinaryValue(element),
                "json" => element.GetRawText(),
                _ => ConvertFallbackValue(element)
            };
        }

        private static object ConvertTinyIntValue(JsonElement element, string columnType)
        {
            if (columnType.StartsWith("tinyint(1)", StringComparison.OrdinalIgnoreCase))
            {
                if (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False)
                {
                    return element.GetBoolean();
                }
            }

            return element.GetInt32();
        }

        private static object ConvertFallbackValue(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                return element.GetString() ?? string.Empty;
            }

            if (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False)
            {
                return element.GetBoolean();
            }

            if (element.ValueKind == JsonValueKind.Number)
            {
                if (element.TryGetInt64(out var integerValue))
                {
                    return integerValue;
                }

                if (element.TryGetDecimal(out var decimalValue))
                {
                    return decimalValue;
                }

                return element.GetDouble();
            }

            return element.GetRawText();
        }

        private static object ParseDateTimeValue(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                return ParseProviderDateTimeObject(element);
            }

            if (element.ValueKind != JsonValueKind.String)
            {
                var rawText = ReadElementAsString(element);
                if (!string.IsNullOrWhiteSpace(rawText) &&
                    DateTime.TryParse(rawText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedDateTime))
                {
                    return parsedDateTime;
                }

                return element.GetDateTime();
            }

            var value = ReadElementAsString(element);
            if (string.IsNullOrWhiteSpace(value))
            {
                return DBNull.Value;
            }

            return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        private static object ParseProviderDateTimeObject(JsonElement element)
        {
            if (TryGetProperty(element, "IsValidDateTime", out var isValidElement) &&
                isValidElement.ValueKind == JsonValueKind.False)
            {
                return DBNull.Value;
            }

            var year = GetRequiredInt32(element, "Year");
            var month = GetRequiredInt32(element, "Month");
            var day = GetRequiredInt32(element, "Day");
            var hour = GetOptionalInt32(element, "Hour");
            var minute = GetOptionalInt32(element, "Minute");
            var second = GetOptionalInt32(element, "Second");
            var millisecond = GetOptionalInt32(element, "Millisecond");
            var microsecond = GetOptionalInt32(element, "Microsecond");

            return BuildDateTime(year, month, day, hour, minute, second, millisecond, microsecond);
        }

        private static byte[] ParseBinaryValue(JsonElement element)
        {
            var base64Value = ReadElementAsString(element);
            return string.IsNullOrEmpty(base64Value)
                ? Array.Empty<byte>()
                : Convert.FromBase64String(base64Value);
        }

        private static string? ReadElementAsString(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Object or JsonValueKind.Array => element.GetRawText(),
                _ => element.ToString()
            };
        }

        private static object? NormalizeBackupValue(object value)
        {
            if (TryNormalizeProviderDateTime(value, out var normalized))
            {
                return normalized;
            }

            return value;
        }

        private static bool TryNormalizeProviderDateTime(object value, out object? normalized)
        {
            var valueType = value.GetType();
            if (!string.Equals(valueType.Name, "MySqlDateTime", StringComparison.Ordinal))
            {
                normalized = null;
                return false;
            }

            var isValidProperty = valueType.GetProperty("IsValidDateTime");
            if (isValidProperty?.PropertyType == typeof(bool) && !(bool)isValidProperty.GetValue(value)!)
            {
                normalized = null;
                return true;
            }

            var year = GetReflectedInt32(valueType, value, "Year");
            var month = GetReflectedInt32(valueType, value, "Month");
            var day = GetReflectedInt32(valueType, value, "Day");
            var hour = GetReflectedInt32(valueType, value, "Hour");
            var minute = GetReflectedInt32(valueType, value, "Minute");
            var second = GetReflectedInt32(valueType, value, "Second");
            var millisecond = GetReflectedInt32(valueType, value, "Millisecond");
            var microsecond = GetReflectedInt32(valueType, value, "Microsecond");

            normalized = BuildDateTime(year, month, day, hour, minute, second, millisecond, microsecond)
                .ToString("O", CultureInfo.InvariantCulture);
            return true;
        }

        private static DateTime BuildDateTime(
            int year,
            int month,
            int day,
            int hour,
            int minute,
            int second,
            int millisecond,
            int microsecond)
        {
            var dateTime = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified)
                .AddMilliseconds(millisecond);

            var subMillisecondMicroseconds = microsecond % 1000;
            if (subMillisecondMicroseconds > 0)
            {
                dateTime = dateTime.AddTicks(subMillisecondMicroseconds * 10L);
            }

            return dateTime;
        }

        private static int GetReflectedInt32(Type valueType, object instance, string propertyName)
        {
            var property = valueType.GetProperty(propertyName)
                ?? throw new InvalidOperationException($"Backup value type `{valueType.FullName}` is missing property `{propertyName}`.");

            return Convert.ToInt32(property.GetValue(instance), CultureInfo.InvariantCulture);
        }

        private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        private static int GetRequiredInt32(JsonElement element, string propertyName)
        {
            if (!TryGetProperty(element, propertyName, out var value))
            {
                throw new InvalidOperationException($"Backup datetime payload is missing `{propertyName}`.");
            }

            return value.GetInt32();
        }

        private static int GetOptionalInt32(JsonElement element, string propertyName)
        {
            return TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.Number
                ? value.GetInt32()
                : 0;
        }

        private static async Task ExecuteNonQueryAsync(
            MySqlConnection connection,
            MySqlTransaction transaction,
            string sql,
            CancellationToken cancellationToken)
        {
            await using var command = new MySqlCommand(sql, connection, transaction);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static string EscapeIdentifier(string identifier)
        {
            return $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`";
        }
    }
}
