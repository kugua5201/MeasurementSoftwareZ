using MeasurementSoftware.Extensions;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Logs;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace MeasurementSoftware.Services
{
    /// <summary>
    /// 数据记录服务（SQLite 持久化实现）。
    /// 按年分库保存，用于检测记录与 SPC 分析。
    /// </summary>
    public class DataRecordService : IDataRecordService
    {
        private const string TableName = "MeasurementRecords";
        private readonly ILogService _log;
        private readonly string _databaseFolder;
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        public DataRecordService(ILogService log)
        {
            _log = log;
            _databaseFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DataRecords");
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                Directory.CreateDirectory(_databaseFolder);
                await EnsureDatabaseAsync(DateTime.Now.Year);
                _log.Info($"数据记录服务已初始化（SQLite）：{_databaseFolder}");
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"初始化 SQLite 数据记录服务失败: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SaveRecordAsync(MeasurementRecord record)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(record);

                await EnsureDatabaseAsync(record.MeasurementTime.Year);
                await using var connection = CreateConnection(record.MeasurementTime.Year);
                await connection.OpenAsync();

                await using var command = connection.CreateCommand();
                command.CommandText = $@"
INSERT INTO {TableName}
(RecipeName, MeasurementTime, Barcode, OperatorName, IsStepMeasurement, StepNumber, TotalSteps, OverallResultText, Remarks, ChannelDataJson)
VALUES
($RecipeName, $MeasurementTime, $Barcode, $OperatorName, $IsStepMeasurement, $StepNumber, $TotalSteps, $OverallResultText, $Remarks, $ChannelDataJson);";

                command.Parameters.AddWithValue("$RecipeName", record.RecipeName ?? string.Empty);
                command.Parameters.AddWithValue("$MeasurementTime", record.MeasurementTime.ToString("O", CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue("$Barcode", record.Barcode ?? string.Empty);
                command.Parameters.AddWithValue("$OperatorName", record.OperatorName ?? string.Empty);
                command.Parameters.AddWithValue("$IsStepMeasurement", record.IsStepMeasurement ? 1 : 0);
                command.Parameters.AddWithValue("$StepNumber", record.StepNumber);
                command.Parameters.AddWithValue("$TotalSteps", record.TotalSteps);
                command.Parameters.AddWithValue("$OverallResultText", record.OverallResult.GetDescription());
                command.Parameters.AddWithValue("$Remarks", record.Remarks ?? string.Empty);
                command.Parameters.AddWithValue("$ChannelDataJson", JsonSerializer.Serialize(record.ChannelData, _jsonOptions));
                await command.ExecuteNonQueryAsync();

                _log.Info($"测量记录已保存到 SQLite: {record.RecordId}");
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"测量记录保存到 SQLite 失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 根据配方中的存储规则将测量结果写入 CSV 文件。
        /// </summary>
        public async Task<bool> SaveRecordToConfiguredFileAsync(MeasurementRecord record, MeasurementRecipe recipe)
        {
            try
            {
                var storage = recipe.OtherSettings.AcquisitionStorage;
                if (!storage.AutoSaveEnabled)
                {
                    return true;
                }

                var baseFolder = string.IsNullOrWhiteSpace(storage.OutputFolder)
                    ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AcquisitionRecords")
                    : storage.OutputFolder;

                Directory.CreateDirectory(baseFolder);

                var baseFileName = BuildFileName(storage, recipe, record);
                const string extension = ".csv";
                var targetFile = ResolveOutputFilePath(baseFolder, baseFileName, extension, storage.MaxFileSizeMb);

                await AppendCsvRecordAsync(targetFile, record, storage.CsvColumns, storage.ExportSingleLinePerMeasurement);

                _log.Info($"采集记录已按规则写入: {targetFile}");
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"采集记录文件写入失败: {ex.Message}");
                return false;
            }
        }

        public async Task<List<MeasurementRecord>> QueryRecordsAsync(DateTime startDate, DateTime endDate, string? recipeName = null, string? barcode = null)
        {
            try
            {
                if (startDate > endDate)
                {
                    (startDate, endDate) = (endDate, startDate);
                }

                var results = new List<MeasurementRecord>();
                foreach (var databaseFile in EnumerateDatabaseFiles(startDate, endDate))
                {
                    await using var connection = new SqliteConnection($"Data Source={databaseFile}");
                    await connection.OpenAsync();

                    await using var command = connection.CreateCommand();
                    command.CommandText = $@"
SELECT * FROM {TableName}
WHERE MeasurementTime >= $StartDate AND MeasurementTime <= $EndDate
  AND (
        ($RecipeName = '' AND $Barcode = '')
        OR ($RecipeName <> '' AND $Barcode = '' AND RecipeName LIKE $RecipeNameLike)
        OR ($RecipeName = '' AND $Barcode <> '' AND Barcode = $BarcodeExact)
        OR ($RecipeName <> '' AND $Barcode <> '' AND (RecipeName LIKE $RecipeNameLike OR Barcode = $BarcodeExact))
      )
ORDER BY MeasurementTime DESC;";
                    command.Parameters.AddWithValue("$StartDate", startDate.ToString("O", CultureInfo.InvariantCulture));
                    command.Parameters.AddWithValue("$EndDate", endDate.ToString("O", CultureInfo.InvariantCulture));
                    command.Parameters.AddWithValue("$RecipeName", recipeName ?? string.Empty);
                    command.Parameters.AddWithValue("$RecipeNameLike", $"%{recipeName?.Trim() ?? string.Empty}%");
                    command.Parameters.AddWithValue("$Barcode", barcode ?? string.Empty);
                    command.Parameters.AddWithValue("$BarcodeExact", barcode?.Trim() ?? string.Empty);

                    await using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        results.Add(MapRecord(reader));
                    }
                }

                return results.OrderByDescending(r => r.MeasurementTime).ToList();
            }
            catch (Exception ex)
            {
                _log.Error($"查询测量记录失败: {ex.Message}");
                return [];
            }
        }

        public Task<List<MeasurementRecord>> QueryRecordsByYearAsync(int year)
        {
            var startDate = new DateTime(year, 1, 1);
            var endDate = new DateTime(year, 12, 31, 23, 59, 59);
            return QueryRecordsAsync(startDate, endDate);
        }

        public Task<List<MeasurementRecord>> QueryRecordsByMonthAsync(int year, int month)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddSeconds(-1);
            return QueryRecordsAsync(startDate, endDate);
        }

        public Task<List<MeasurementRecord>> QueryRecordsByDayAsync(DateTime date)
        {
            var startDate = date.Date;
            var endDate = date.Date.AddDays(1).AddSeconds(-1);
            return QueryRecordsAsync(startDate, endDate);
        }

        public Task<List<MeasurementRecord>> QueryRecordsByBarcodeAsync(string barcode)
        {
            return QueryRecordsAsync(DateTime.MinValue, DateTime.MaxValue, barcode: barcode);
        }

        public async Task<bool> ExportToCsvAsync(List<MeasurementRecord> records, string filePath)
        {
            try
            {
                var csv = new StringBuilder();
                var orderedRecords = records.OrderByDescending(r => r.MeasurementTime).ToList();
                var maxChannelCount = orderedRecords.Count == 0 ? 0 : orderedRecords.Max(r => r.ChannelData?.Count ?? 0);

                var headers = new List<string>
                {
                    "配方名称",
                    "测量时间",
                    "测量结果",
                    "二维码",
                    "操作员",
                    "是否启用工步",
                    "工步数"
                };

                for (var i = 1; i <= maxChannelCount; i++)
                {
                    headers.AddRange([
                        $"通道{i}_通道编号",
                        $"通道{i}_通道名称",
                        $"通道{i}_通道说明",
                        $"通道{i}_工步编号",
                        $"通道{i}_工步名称",
                        $"通道{i}_通道类型",
                        $"通道{i}_测量类型",
                        $"通道{i}_PLC设备",
                        $"通道{i}_数据点位",
                        $"通道{i}_点位地址",
                        $"通道{i}_是否启用",
                        $"通道{i}_标准值",
                        $"通道{i}_上公差",
                        $"通道{i}_下公差",
                        $"通道{i}_测量值",
                        $"通道{i}_单位",
                        $"通道{i}_小数位",
                        $"通道{i}_是否校准",
                        $"通道{i}_校准方式",
                        $"通道{i}_系数A",
                        $"通道{i}_系数B",
                        $"通道{i}_是否缓存",
                        $"通道{i}_采样数量",
                        $"通道{i}_通道结果"
                    ]);
                }

                csv.AppendLine(string.Join(",", headers.Select(QuoteCsv)));

                foreach (var record in orderedRecords)
                {
                    var row = new List<string>
                    {
                        QuoteCsv(record.RecipeName),
                        QuoteCsv(record.MeasurementTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                        QuoteCsv(record.OverallResult.GetDescription()),
                        QuoteCsv(record.Barcode),
                        QuoteCsv(record.OperatorName),
                        QuoteCsv(record.IsStepMeasurement ? "是" : "否"),
                        QuoteCsv(record.IsStepMeasurement ? $"{record.StepNumber}/{record.TotalSteps}" : string.Empty)
                    };

                    var channels = (record.ChannelData ?? [])
                        .OrderBy(c => c.ChannelNumber)
                        .ToList();

                    for (var i = 0; i < maxChannelCount; i++)
                    {
                        if (i < channels.Count)
                        {
                            var channel = channels[i];
                            row.AddRange([
                                QuoteCsv(channel.ChannelNumberDisplay),
                                QuoteCsv(channel.ChannelName),
                                QuoteCsv(channel.ChannelDescription),
                                QuoteCsv(channel.StepNumberDisplay),
                                QuoteCsv(channel.StepName),
                                QuoteCsv(channel.ChannelType),
                                QuoteCsv(channel.MeasurementType),
                                QuoteCsv(channel.PlcDeviceName),
                                QuoteCsv(channel.DataPointName),
                                QuoteCsv(channel.DataSourceAddress),
                                QuoteCsv(channel.IsEnabledText),
                                QuoteCsv(channel.StandardValue.ToString(CultureInfo.InvariantCulture)),
                                QuoteCsv(channel.UpperTolerance.ToString(CultureInfo.InvariantCulture)),
                                QuoteCsv(channel.LowerTolerance.ToString(CultureInfo.InvariantCulture)),
                                QuoteCsv(channel.MeasuredResultValue.ToString(CultureInfo.InvariantCulture)),
                                QuoteCsv(channel.Unit),
                                QuoteCsv(channel.DecimalPlaces.ToString(CultureInfo.InvariantCulture)),
                                QuoteCsv(channel.RequiresCalibrationText),
                                QuoteCsv(channel.CalibrationModeText),
                                QuoteCsv(channel.CalibrationCoefficientA.ToString(CultureInfo.InvariantCulture)),
                                QuoteCsv(channel.CalibrationCoefficientB.ToString(CultureInfo.InvariantCulture)),
                                QuoteCsv(channel.UseCacheValueText),
                                QuoteCsv(channel.SampleCount.ToString(CultureInfo.InvariantCulture)),
                                QuoteCsv(channel.ResultText)
                            ]);
                        }
                        else
                        {
                            row.AddRange(Enumerable.Repeat(QuoteCsv(string.Empty), 24));
                        }
                    }

                    csv.AppendLine(string.Join(",", row));
                }

                await File.WriteAllTextAsync(filePath, csv.ToString(), Encoding.UTF8);
                _log.Info($"导出CSV成功: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"导出CSV失败: {ex.Message}");
                return false;
            }
        }

        public Task<bool> ExportToExcelAsync(List<MeasurementRecord> records, string filePath)
        {
            _log.Info("[占位] Excel导出功能待实现");
            return Task.FromResult(false);
        }

        public async Task<bool> DeleteRecordAsync(string recordId)
        {
            var deleted = false;
            foreach (var databaseFile in EnumerateAllDatabaseFiles())
            {
                await using var connection = new SqliteConnection($"Data Source={databaseFile}");
                await connection.OpenAsync();

                await using var command = connection.CreateCommand();
                if (!int.TryParse(recordId, out var id))
                {
                    continue;
                }

                command.CommandText = $"DELETE FROM {TableName} WHERE Id = $Id;";
                command.Parameters.AddWithValue("$Id", id);
                deleted |= await command.ExecuteNonQueryAsync() > 0;
            }

            if (deleted)
            {
                _log.Info($"记录已删除: {recordId}");
            }

            return deleted;
        }

        public async Task<int> CleanupOldRecordsAsync(int daysToKeep)
        {
            var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
            var deletedCount = 0;

            foreach (var databaseFile in EnumerateAllDatabaseFiles())
            {
                await using var connection = new SqliteConnection($"Data Source={databaseFile}");
                await connection.OpenAsync();

                await using var command = connection.CreateCommand();
                command.CommandText = $"DELETE FROM {TableName} WHERE MeasurementTime < $CutoffDate;";
                command.Parameters.AddWithValue("$CutoffDate", cutoffDate.ToString("O", CultureInfo.InvariantCulture));
                deletedCount += await command.ExecuteNonQueryAsync();
            }

            _log.Info($"清理完成，删除 {deletedCount} 条过期记录");
            return deletedCount;
        }

        private async Task EnsureDatabaseAsync(int year)
        {
            var databaseFilePath = GetDatabaseFilePath(year);
            if (File.Exists(databaseFilePath))
            {
                return;
            }

            Directory.CreateDirectory(_databaseFolder);
            using var connection = CreateConnection(year);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = $@"
            CREATE TABLE IF NOT EXISTS {TableName}
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RecipeName TEXT NOT NULL,
                MeasurementTime TEXT NOT NULL,
                Barcode TEXT NULL,
                OperatorName TEXT NULL,
                IsStepMeasurement INTEGER NOT NULL DEFAULT 0,
                StepNumber INTEGER NOT NULL DEFAULT 1,
                TotalSteps INTEGER NOT NULL DEFAULT 1,
                OverallResultText TEXT NOT NULL,
                Remarks TEXT NULL,
                ChannelDataJson TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_MeasurementRecords_RecipeName ON MeasurementRecords(RecipeName);
            CREATE INDEX IF NOT EXISTS IX_MeasurementRecords_MeasurementTime ON MeasurementRecords(MeasurementTime);
            CREATE INDEX IF NOT EXISTS IX_MeasurementRecords_Barcode ON MeasurementRecords(Barcode);";
            await command.ExecuteNonQueryAsync();

            await EnsureColumnAsync(connection, "OperatorName", "TEXT NULL");
            await EnsureColumnAsync(connection, "IsStepMeasurement", "INTEGER NOT NULL DEFAULT 0");
            await EnsureColumnAsync(connection, "StepNumber", "INTEGER NOT NULL DEFAULT 1");
            await EnsureColumnAsync(connection, "TotalSteps", "INTEGER NOT NULL DEFAULT 1");
            await connection.CloseAsync();
            //Microsoft.Data.Sqlite.SqliteConnection.ClearPool(connection);
            command?.Dispose();
        }

        private static async Task EnsureColumnAsync(SqliteConnection connection, string columnName, string columnDefinition)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({TableName});";

            var exists = false;
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
            }
            command?.Dispose();
            if (exists)
            {
                return;
            }

            await using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = $"ALTER TABLE {TableName} ADD COLUMN {columnName} {columnDefinition};";
            await alterCommand.ExecuteNonQueryAsync();
            alterCommand?.Dispose();
        }

        private SqliteConnection CreateConnection(int year)
        {
            return new SqliteConnection($"Data Source={GetDatabaseFilePath(year)};Pooling=False");
            //  return new SqliteConnection($"Data Source={GetDatabaseFilePath(year)}");
        }

        private string GetDatabaseFilePath(int year)
        {
            return Path.Combine(_databaseFolder, $"MeasurementRecords_{year}.db");
        }

        private IEnumerable<string> EnumerateDatabaseFiles(DateTime startDate, DateTime endDate)
        {
            if (startDate == DateTime.MinValue || endDate == DateTime.MaxValue)
            {
                return EnumerateAllDatabaseFiles();
            }

            var files = new List<string>();
            for (var year = startDate.Year; year <= endDate.Year; year++)
            {
                var file = GetDatabaseFilePath(year);
                if (File.Exists(file))
                {
                    files.Add(file);
                }
            }

            return files;
        }

        private IEnumerable<string> EnumerateAllDatabaseFiles()
        {
            if (!Directory.Exists(_databaseFolder))
            {
                return [];
            }

            return Directory.EnumerateFiles(_databaseFolder, "MeasurementRecords_*.db", SearchOption.TopDirectoryOnly);
        }

        private MeasurementRecord MapRecord(SqliteDataReader reader)
        {
            var measurementTimeText = GetString(reader, "MeasurementTime");
            var measurementTime = string.IsNullOrWhiteSpace(measurementTimeText)
                ? DateTime.Now
                : DateTime.Parse(measurementTimeText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            var channelDataJson = GetString(reader, "ChannelDataJson");

            return new MeasurementRecord
            {
                RecordId = GetInt(reader, "Id").ToString(CultureInfo.InvariantCulture),
                RecipeName = GetString(reader, "RecipeName"),
                MeasurementTime = measurementTime,
                OverallResult = ParseOverallResult(GetString(reader, "OverallResultText")),
                Barcode = GetString(reader, "Barcode"),
                OperatorName = GetString(reader, "OperatorName"),
                IsStepMeasurement = GetInt(reader, "IsStepMeasurement") == 1,
                StepNumber = GetInt(reader, "StepNumber", 1),
                TotalSteps = GetInt(reader, "TotalSteps", 1),
                Remarks = GetString(reader, "Remarks"),
                ChannelData = JsonSerializer.Deserialize<List<ChannelMeasurementData>>(channelDataJson, _jsonOptions) ?? []
            };
        }

        private static MeasurementResult ParseOverallResult(string? text)
        {
            return text switch
            {
                "OK" or "合格" => MeasurementResult.Pass,
                "NG" or "不合格" => MeasurementResult.Fail,
                _ => MeasurementResult.NotMeasured
            };
        }

        private static string GetString(SqliteDataReader reader, string columnName, string defaultValue = "")
        {
            var ordinal = GetOrdinal(reader, columnName);
            if (ordinal < 0 || reader.IsDBNull(ordinal))
            {
                return defaultValue;
            }

            return reader.GetValue(ordinal)?.ToString() ?? defaultValue;
        }

        private static int GetInt(SqliteDataReader reader, string columnName, int defaultValue = 0)
        {
            var ordinal = GetOrdinal(reader, columnName);
            if (ordinal < 0 || reader.IsDBNull(ordinal))
            {
                return defaultValue;
            }

            var value = reader.GetValue(ordinal);
            return value switch
            {
                int intValue => intValue,
                long longValue => (int)longValue,
                string stringValue when int.TryParse(stringValue, out var parsed) => parsed,
                _ => defaultValue
            };
        }

        private static int GetOrdinal(SqliteDataReader reader, string columnName)
        {
            for (var i = 0; i < reader.FieldCount; i++)
            {
                if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string BuildFileName(AcquisitionStorageConfig storage, MeasurementRecipe recipe, MeasurementRecord record)
        {
            var parts = new List<string>
            {
                string.IsNullOrWhiteSpace(storage.FileBaseName) ? "测量记录" : storage.FileBaseName.Trim()
            };

            if (storage.AppendRecipeNameToFileName && !string.IsNullOrWhiteSpace(recipe.BasicInfo.RecipeName))
            {
                parts.Add(recipe.BasicInfo.RecipeName.Trim());
            }

            if (storage.AppendDateToFileName)
            {
                parts.Add(record.MeasurementTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            }

            if (storage.AppendTimeToFileName)
            {
                parts.Add(record.MeasurementTime.ToString("HHmmss", CultureInfo.InvariantCulture));
            }

            if (storage.AppendBarcodeToFileName && !string.IsNullOrWhiteSpace(record.Barcode))
            {
                parts.Add(record.Barcode.Trim());
            }

            var fileName = string.Join("_", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(invalidChar, '_');
            }

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return fileName;
            }

            if (!string.IsNullOrWhiteSpace(storage.FileNamePattern))
            {
                return storage.FileNamePattern;
            }

            return $"Record_{record.MeasurementTime:yyyyMMdd_HHmmss}";
        }

        private static string ResolveOutputFilePath(string folder, string baseFileName, string extension, int maxFileSizeMb)
        {
            var maxBytes = Math.Max(1, maxFileSizeMb) * 1024L * 1024L;
            var filePath = Path.Combine(folder, baseFileName + extension);
            var index = 1;

            while (File.Exists(filePath) && new FileInfo(filePath).Length >= maxBytes)
            {
                filePath = Path.Combine(folder, $"{baseFileName}_{index}{extension}");
                index++;
            }

            return filePath;
        }

        private static async Task AppendCsvRecordAsync(string filePath, MeasurementRecord record, IEnumerable<AcquisitionCsvColumnConfig> csvColumns, bool exportSingleLinePerMeasurement)
        {
            var columns = GetConfiguredColumns(csvColumns).ToList();
            if (columns.Count == 0)
            {
                columns = GetDefaultColumns().ToList();
            }

            var builder = new StringBuilder();
            if (exportSingleLinePerMeasurement)
            {
                AppendSingleLineCsvRecord(filePath, record, columns, builder);
            }
            else
            {
                AppendMultiLineCsvRecord(filePath, record, columns, builder);
            }

            await File.AppendAllTextAsync(filePath, builder.ToString(), Encoding.UTF8);
        }

        private static void AppendMultiLineCsvRecord(string filePath, MeasurementRecord record, List<CsvColumnDefinition> columns, StringBuilder builder)
        {
            if (!File.Exists(filePath))
            {
                builder.AppendLine(string.Join(",", columns.Select(c => QuoteCsv(c.Header))));
            }

            foreach (var channel in record.ChannelData)
            {
                var values = columns.Select(column => QuoteCsv(column.GetValue(record, channel, 0)));
                builder.AppendLine(string.Join(",", values));
            }
        }

        private static void AppendSingleLineCsvRecord(string filePath, MeasurementRecord record, List<CsvColumnDefinition> columns, StringBuilder builder)
        {
            var recordColumns = columns.Where(c => c.Scope == CsvColumnScope.Record).ToList();
            var channelColumns = columns.Where(c => c.Scope == CsvColumnScope.Channel).ToList();

            if (!File.Exists(filePath))
            {
                var headers = new List<string>();
                headers.AddRange(recordColumns.Select(c => QuoteCsv(c.Header)));
                for (var i = 0; i < record.ChannelData.Count; i++)
                {
                    var channelPrefix = $"通道{i + 1}";
                    headers.AddRange(channelColumns.Select(c => QuoteCsv($"{channelPrefix}_{c.Header}")));
                }

                builder.AppendLine(string.Join(",", headers));
            }

            var row = new List<string>();
            row.AddRange(recordColumns.Select(c => QuoteCsv(c.GetValue(record, null, 0))));
            for (var i = 0; i < record.ChannelData.Count; i++)
            {
                var channel = record.ChannelData[i];
                row.AddRange(channelColumns.Select(c => QuoteCsv(c.GetValue(record, channel, i + 1))));
            }

            builder.AppendLine(string.Join(",", row));
        }

        private static IEnumerable<CsvColumnDefinition> GetConfiguredColumns(IEnumerable<AcquisitionCsvColumnConfig>? csvColumns)
        {
            if (csvColumns == null)
            {
                return [];
            }

            var definitions = GetDefaultColumns().ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);
            return csvColumns
                .Where(c => !string.IsNullOrWhiteSpace(c.Key) && definitions.ContainsKey(c.Key))
                .Select(c => definitions[c.Key] with { Header = string.IsNullOrWhiteSpace(c.Header) ? definitions[c.Key].Header : c.Header });
        }

        private static IEnumerable<CsvColumnDefinition> GetDefaultColumns()
        {
            return [
                new CsvColumnDefinition("RecipeName", "配方名称", CsvColumnScope.Record, (record, _, _) => record.RecipeName),
                new CsvColumnDefinition("MeasurementTime", "测量时间", CsvColumnScope.Record, (record, _, _) => record.MeasurementTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                new CsvColumnDefinition("Barcode", "二维码", CsvColumnScope.Record, (record, _, _) => record.Barcode),
                new CsvColumnDefinition("IsStepMeasurement", "是否工步测量", CsvColumnScope.Record, (record, _, _) => record.IsStepMeasurement ? "是" : "否"),
                new CsvColumnDefinition("CurrentStepNumber", "当前工步编号", CsvColumnScope.Record, (record, _, _) => record.StepNumber.ToString(CultureInfo.InvariantCulture)),
                new CsvColumnDefinition("TotalSteps", "总工步数", CsvColumnScope.Record, (record, _, _) => record.TotalSteps.ToString(CultureInfo.InvariantCulture)),
                new CsvColumnDefinition("OverallResult", "总结果", CsvColumnScope.Record, (record, _, _) => record.OverallResult.GetDescription()),
                new CsvColumnDefinition("ChannelNumber", "通道编号", CsvColumnScope.Channel, (_, channel, _) => channel?.ChannelNumberDisplay ?? string.Empty),
                new CsvColumnDefinition("ChannelName", "通道名称", CsvColumnScope.Channel, (_, channel, _) => channel?.ChannelName ?? string.Empty),
                new CsvColumnDefinition("ChannelDescription", "通道说明", CsvColumnScope.Channel, (_, channel, _) => channel?.ChannelDescription ?? string.Empty),
                new CsvColumnDefinition("ChannelStepNumber", "通道工步编号", CsvColumnScope.Channel, (_, channel, _) => channel?.StepNumberDisplay ?? string.Empty),
                new CsvColumnDefinition("ChannelStepName", "通道工步名称", CsvColumnScope.Channel, (_, channel, _) => channel?.StepName ?? string.Empty),
                new CsvColumnDefinition("ChannelType", "通道类型", CsvColumnScope.Channel, (_, channel, _) => channel?.ChannelType ?? string.Empty),
                new CsvColumnDefinition("MeasurementMode", "测量模式", CsvColumnScope.Channel, (_, channel, _) => channel?.MeasurementMode ?? string.Empty),
                new CsvColumnDefinition("SourceSummary", "来源摘要", CsvColumnScope.Channel, (_, channel, _) => channel?.SourceSummary ?? string.Empty),
                new CsvColumnDefinition("FormulaScript", "公式脚本", CsvColumnScope.Channel, (_, channel, _) => channel?.FormulaScript ?? string.Empty),
                new CsvColumnDefinition("DataSourceAddress", "数据源地址", CsvColumnScope.Channel, (_, channel, _) => channel?.DataSourceAddress ?? string.Empty),
                new CsvColumnDefinition("PlcDeviceName", "PLC设备", CsvColumnScope.Channel, (_, channel, _) => channel?.PlcDeviceName ?? string.Empty),
                new CsvColumnDefinition("DataPointName", "数据点名称", CsvColumnScope.Channel, (_, channel, _) => channel?.DataPointName ?? string.Empty),
                new CsvColumnDefinition("IsEnabled", "是否启用", CsvColumnScope.Channel, (_, channel, _) => channel?.IsEnabled == true ? "是" : "否"),
                new CsvColumnDefinition("StandardValue", "标准值", CsvColumnScope.Channel, (_, channel, _) => channel?.StandardValue.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                new CsvColumnDefinition("UpperTolerance", "公差上限", CsvColumnScope.Channel, (_, channel, _) => channel?.UpperTolerance.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                new CsvColumnDefinition("LowerTolerance", "公差下限", CsvColumnScope.Channel, (_, channel, _) => channel?.LowerTolerance.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                new CsvColumnDefinition("MeasuredValue", "测量值", CsvColumnScope.Channel, (_, channel, _) => channel?.MeasuredResultValue.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                new CsvColumnDefinition("Unit", "单位", CsvColumnScope.Channel, (_, channel, _) => channel?.Unit ?? string.Empty),
                new CsvColumnDefinition("DecimalPlaces", "小数位数", CsvColumnScope.Channel, (_, channel, _) => channel?.DecimalPlaces.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                new CsvColumnDefinition("RequiresCalibration", "是否校准", CsvColumnScope.Channel, (_, channel, _) => channel?.RequiresCalibration == true ? "是" : "否"),
                new CsvColumnDefinition("CalibrationMode", "校准方式", CsvColumnScope.Channel, (_, channel, _) => channel?.CalibrationMode.GetDescription() ?? string.Empty),
                new CsvColumnDefinition("CalibrationCoefficientA", "校准系数A", CsvColumnScope.Channel, (_, channel, _) => channel?.CalibrationCoefficientA.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                new CsvColumnDefinition("CalibrationCoefficientB", "校准系数B", CsvColumnScope.Channel, (_, channel, _) => channel?.CalibrationCoefficientB.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                new CsvColumnDefinition("UseCacheValue", "是否使用缓存", CsvColumnScope.Channel, (_, channel, _) => channel?.UseCacheValue == true ? "是" : "否"),
                new CsvColumnDefinition("SampleCount", "采样数量", CsvColumnScope.Channel, (_, channel, _) => channel?.SampleCount.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                new CsvColumnDefinition("ChannelResult", "通道结果", CsvColumnScope.Channel, (_, channel, _) => channel?.Result.GetDescription() ?? string.Empty)
            ];
        }

        private static string QuoteCsv(string? value)
        {
            return $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}" + "\"";
        }

        private enum CsvColumnScope
        {
            Record,
            Channel
        }

        private sealed record CsvColumnDefinition(string Key, string Header, CsvColumnScope Scope, Func<MeasurementRecord, ChannelMeasurementData?, int, string> GetValue);
    }
}
