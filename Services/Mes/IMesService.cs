using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services
{
    /// <summary>
    /// MES数据上传服务接口
    /// </summary>
    public interface IMesService
    {
        /// <summary>
        /// 初始化MES连接
        /// </summary>
        Task<bool> InitializeAsync(MesUploadConfig config);

        /// <summary>
        /// 上传测量记录到MES
        /// </summary>
        Task<bool> UploadRecordAsync(MeasurementRecord record);

        /// <summary>
        /// 批量上传测量记录
        /// </summary>
        Task<bool> UploadRecordsAsync(List<MeasurementRecord> records);

        /// <summary>
        /// 测试连接
        /// </summary>
        Task<bool> TestConnectionAsync();

        /// <summary>
        /// 关闭MES连接
        /// </summary>
        Task CloseAsync();

        /// <summary>
        /// 检查是否已连接
        /// </summary>
        bool IsConnected { get; }
    }
}
