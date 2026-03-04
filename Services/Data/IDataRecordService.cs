using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services
{
    /// <summary>
    /// 数据记录服务接口（本地数据库）
    /// </summary>
    public interface IDataRecordService
    {
        /// <summary>
        /// 初始化本地数据库
        /// </summary>
        Task<bool> InitializeAsync();

        /// <summary>
        /// 保存测量记录
        /// </summary>
        Task<bool> SaveRecordAsync(MeasurementRecord record);

        /// <summary>
        /// 查询测量记录
        /// </summary>
        Task<List<MeasurementRecord>> QueryRecordsAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// 按年查询
        /// </summary>
        Task<List<MeasurementRecord>> QueryRecordsByYearAsync(int year);

        /// <summary>
        /// 按月查询
        /// </summary>
        Task<List<MeasurementRecord>> QueryRecordsByMonthAsync(int year, int month);

        /// <summary>
        /// 按日查询
        /// </summary>
        Task<List<MeasurementRecord>> QueryRecordsByDayAsync(DateTime date);

        /// <summary>
        /// 按二维码查询
        /// </summary>
        Task<List<MeasurementRecord>> QueryRecordsByBarcodeAsync(string barcode);

        /// <summary>
        /// 导出记录到CSV
        /// </summary>
        Task<bool> ExportToCsvAsync(List<MeasurementRecord> records, string filePath);

        /// <summary>
        /// 导出记录到Excel
        /// </summary>
        Task<bool> ExportToExcelAsync(List<MeasurementRecord> records, string filePath);

        /// <summary>
        /// 删除记录
        /// </summary>
        Task<bool> DeleteRecordAsync(string recordId);

        /// <summary>
        /// 清理过期记录
        /// </summary>
        Task<int> CleanupOldRecordsAsync(int daysToKeep);
    }
}
