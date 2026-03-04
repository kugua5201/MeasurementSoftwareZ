using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Logs;
using System.Text;
using System.IO;

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
    }
}
