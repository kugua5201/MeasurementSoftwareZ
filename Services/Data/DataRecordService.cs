using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Logs;
using System.IO;
using System.Globalization;
using System.Text;
using MeasurementSoftware.Extensions;

namespace MeasurementSoftware.Services
{
    /// <summary>
    /// 数据记录服务（内存存储占位实现）
    /// TODO: 后续使用 SQLite 或其他数据库实现
    /// </summary>
    public class DataRecordService : IDataRecordService
    {
        private readonly ILog _log;
        private readonly List<MeasurementRecord> _records = new();

        public DataRecordService(ILog log)
        {
            _log = log;
        }

        public async Task<bool> InitializeAsync()
        {
            _log.Info("数据记录服务已初始化（内存存储）");
            return await Task.FromResult(true);
        }

        public async Task<bool> SaveRecordAsync(MeasurementRecord record)
        {
            _records.Add(record);
            _log.Info($"测量记录已保存（内存）: {record.RecordId}");
            return await Task.FromResult(true);
        }

        /// <summary>
        /// 根据配方中的存储规则将测量结果写入 CSV 文件。
        /// 文件达到上限后自动追加递增编号，避免持续写入同一个超大文件。
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

                var baseFolder = string.IsNullOrWhiteSpace(storage.OutputFolder) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AcquisitionRecords") : storage.OutputFolder;

                Directory.CreateDirectory(baseFolder);

                var baseFileName = BuildFileName(storage.FileNamePattern, recipe, record.MeasurementTime);
                const string extension = ".csv";
                var targetFile = ResolveOutputFilePath(baseFolder, baseFileName, extension, storage.MaxFileSizeMb);

                await AppendCsvRecordAsync(targetFile, record, storage.CsvColumns);

                _log.Info($"采集记录已按规则写入: {targetFile}");
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"采集记录文件写入失败: {ex.Message}");
                return false;
            }
        }

        public async Task<List<MeasurementRecord>> QueryRecordsAsync(DateTime startDate, DateTime endDate)
        {
            var results = _records
                .Where(r => r.MeasurementTime >= startDate && r.MeasurementTime <= endDate)
                .ToList();
            return await Task.FromResult(results);
        }

        public async Task<List<MeasurementRecord>> QueryRecordsByYearAsync(int year)
        {
            var startDate = new DateTime(year, 1, 1);
            var endDate = new DateTime(year, 12, 31, 23, 59, 59);
            return await QueryRecordsAsync(startDate, endDate);
        }

        public async Task<List<MeasurementRecord>> QueryRecordsByMonthAsync(int year, int month)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddSeconds(-1);
            return await QueryRecordsAsync(startDate, endDate);
        }

        public async Task<List<MeasurementRecord>> QueryRecordsByDayAsync(DateTime date)
        {
            var startDate = date.Date;
            var endDate = date.Date.AddDays(1).AddSeconds(-1);
            return await QueryRecordsAsync(startDate, endDate);
        }

        public async Task<List<MeasurementRecord>> QueryRecordsByBarcodeAsync(string barcode)
        {
            var results = _records.Where(r => r.Barcode == barcode).ToList();
            return await Task.FromResult(results);
        }

        public async Task<bool> ExportToCsvAsync(List<MeasurementRecord> records, string filePath)
        {
            try
            {
                var csv = new StringBuilder();
                csv.AppendLine("记录ID,配方名称,测量时间,测量结果,操作员,二维码,备注");

                foreach (var record in records)
                {
                    csv.AppendLine($"\"{record.RecordId}\",\"{record.RecipeName}\"," +
                                 $"\"{record.MeasurementTime:yyyy-MM-dd HH:mm:ss}\",\"{record.OverallResult}\"," +
                                 $"\"{record.OperatorName}\",\"{record.Barcode}\",\"{record.Remarks}\"");
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

        public async Task<bool> ExportToExcelAsync(List<MeasurementRecord> records, string filePath)
        {
            _log.Info("[占位] Excel导出功能待实现");
            return await Task.FromResult(false);
        }

        public async Task<bool> DeleteRecordAsync(string recordId)
        {
            var record = _records.FirstOrDefault(r => r.RecordId == recordId);
            if (record != null)
            {
                _records.Remove(record);
                _log.Info($"记录已删除: {recordId}");
                return await Task.FromResult(true);
            }
            return await Task.FromResult(false);
        }

        public async Task<int> CleanupOldRecordsAsync(int daysToKeep)
        {
            var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
            var oldRecords = _records.Where(r => r.MeasurementTime < cutoffDate).ToList();

            foreach (var record in oldRecords)
            {
                _records.Remove(record);
            }

            _log.Info($"清理完成，删除 {oldRecords.Count} 条过期记录");
            return await Task.FromResult(oldRecords.Count);
        }

        private static string BuildFileName(string pattern, MeasurementRecipe recipe, DateTime timestamp)
        {
            var fileName = string.IsNullOrWhiteSpace(pattern)
                ? "{RecipeName}"
                : pattern;

            fileName = fileName
                .Replace("{RecipeName}", recipe.BasicInfo.RecipeName)
                .Replace("{RecipeId}", recipe.BasicInfo.RecipeId);

            fileName = System.Text.RegularExpressions.Regex.Replace(fileName, "\\{Time(?::(?<fmt>[^}]+))?\\}", match =>
            {
                var format = match.Groups["fmt"].Success ? match.Groups["fmt"].Value : "yyyyMMdd_HHmmss";
                return timestamp.ToString(format, CultureInfo.InvariantCulture);
            });

            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(invalidChar, '_');
            }

            return string.IsNullOrWhiteSpace(fileName) ? $"Record_{timestamp:yyyyMMdd_HHmmss}" : fileName;
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

        private static async Task AppendCsvRecordAsync(string filePath, MeasurementRecord record, IEnumerable<AcquisitionCsvColumnConfig> csvColumns)
        {
            var columns = GetConfiguredColumns(csvColumns).ToList();
            if (columns.Count == 0)
            {
                columns = GetDefaultColumns().ToList();
            }

            var builder = new StringBuilder();
            if (!File.Exists(filePath))
            {
                builder.AppendLine(string.Join(",", columns.Select(c => QuoteCsv(c.Header))));
            }

            foreach (var channel in record.ChannelData)
            {
                var values = columns.Select(column => QuoteCsv(column.GetValue(record, channel)));
                builder.AppendLine(string.Join(",", values));
            }

            await File.AppendAllTextAsync(filePath, builder.ToString(), Encoding.UTF8);
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
                new CsvColumnDefinition("RecipeName", "配方名称", (record, _) => record.RecipeName),
                new CsvColumnDefinition("MeasurementTime", "测量时间", (record, _) => record.MeasurementTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                new CsvColumnDefinition("IsStepMeasurement", "是否工步测量", (record, _) => record.IsStepMeasurement ? "是" : "否"),
                new CsvColumnDefinition("CurrentStepNumber", "当前工步编号", (record, _) => record.StepNumber.ToString(CultureInfo.InvariantCulture)),
                new CsvColumnDefinition("TotalSteps", "总工步数", (record, _) => record.TotalSteps.ToString(CultureInfo.InvariantCulture)),
                new CsvColumnDefinition("OverallResult", "总结果", (record, _) => record.OverallResult.GetDescription()),
                new CsvColumnDefinition("ChannelNumber", "通道编号", (_, channel) => channel.ChannelNumber.ToString(CultureInfo.InvariantCulture)),
                new CsvColumnDefinition("ChannelName", "通道名称", (_, channel) => channel.ChannelName),
                new CsvColumnDefinition("ChannelDescription", "通道说明", (_, channel) => channel.ChannelDescription),
                new CsvColumnDefinition("ChannelStepNumber", "通道工步编号", (_, channel) => channel.StepNumber.ToString(CultureInfo.InvariantCulture)),
                new CsvColumnDefinition("ChannelStepName", "通道工步名称", (_, channel) => channel.StepName),
                new CsvColumnDefinition("ChannelType", "通道类型", (_, channel) => channel.ChannelType),
                new CsvColumnDefinition("DataSourceAddress", "数据源地址", (_, channel) => channel.DataSourceAddress),
                new CsvColumnDefinition("PlcDeviceName", "PLC设备", (_, channel) => channel.PlcDeviceName),
                new CsvColumnDefinition("DataPointName", "数据点名称", (_, channel) => channel.DataPointName),
                new CsvColumnDefinition("IsEnabled", "是否启用", (_, channel) => channel.IsEnabled ? "是" : "否"),
                new CsvColumnDefinition("StandardValue", "标准值", (_, channel) => channel.StandardValue.ToString(CultureInfo.InvariantCulture)),
                new CsvColumnDefinition("UpperTolerance", "公差上限", (_, channel) => channel.UpperTolerance.ToString(CultureInfo.InvariantCulture)),
                new CsvColumnDefinition("LowerTolerance", "公差下限", (_, channel) => channel.LowerTolerance.ToString(CultureInfo.InvariantCulture)),
                new CsvColumnDefinition("MeasuredValue", "测量值", (_, channel) => channel.MeasuredValue.ToString(CultureInfo.InvariantCulture)),
                new CsvColumnDefinition("Unit", "单位", (_, channel) => channel.Unit),
                new CsvColumnDefinition("DecimalPlaces", "小数位数", (_, channel) => channel.DecimalPlaces.ToString(CultureInfo.InvariantCulture)),
                new CsvColumnDefinition("RequiresCalibration", "是否校准", (_, channel) => channel.RequiresCalibration ? "是" : "否"),
                new CsvColumnDefinition("CalibrationMode", "校准方式", (_, channel) => channel.CalibrationMode.GetDescription()),
                new CsvColumnDefinition("CalibrationCoefficientA", "校准系数A", (_, channel) => channel.CalibrationCoefficientA.ToString(CultureInfo.InvariantCulture)),
                new CsvColumnDefinition("CalibrationCoefficientB", "校准系数B", (_, channel) => channel.CalibrationCoefficientB.ToString(CultureInfo.InvariantCulture)),
                //new CsvColumnDefinition("LastCalibrationTime", "上次校准时间", (_, channel) => channel.LastCalibrationTime?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty),
                new CsvColumnDefinition("UseCacheValue", "是否使用缓存", (_, channel) => channel.UseCacheValue ? "是" : "否"),
                new CsvColumnDefinition("SampleCount", "采样数量", (_, channel) => channel.SampleCount.ToString(CultureInfo.InvariantCulture)),
                new CsvColumnDefinition("ChannelResult", "通道结果", (_, channel) => channel.Result.GetDescription())
            ];
        }

        private static string QuoteCsv(string? value)
        {
            return $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
        }

        private static string ConvertResult(MeasurementResult result) => result switch
        {
            MeasurementResult.Pass => "OK",
            MeasurementResult.Fail => "NG",
            _ => "未测量"
        };

        private sealed record CsvColumnDefinition(string Key, string Header, Func<MeasurementRecord, ChannelMeasurementData, string> GetValue);
    }
}
