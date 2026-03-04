using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Logs;

namespace MeasurementSoftware.Services
{
    /// <summary>
    /// MES数据上传服务（占位实现）
    /// TODO: 后续使用数据库库实现
    /// </summary>
    public class MesService : IMesService
    {
        private readonly ILog _log;
        private MesUploadConfig? _config;
        private bool _isConnected;

        public bool IsConnected => _isConnected;

        public MesService(ILog log)
        {
            _log = log;
        }

        public async Task<bool> InitializeAsync(MesUploadConfig config)
        {
            _config = config;
            _isConnected = true;
            _log.Info($"MES服务已初始化（占位实现）");
            return await Task.FromResult(true);
        }

        public async Task<bool> UploadRecordAsync(MeasurementRecord record)
        {
            if (_config == null || !IsConnected)
            {
                _log.Error("MES未初始化");
                return false;
            }

            // TODO: 实现实际的上传逻辑
            _log.Info($"[占位] 测量记录上传: {record.RecordId}");
            return await Task.FromResult(true);
        }

        public async Task<bool> UploadRecordsAsync(List<MeasurementRecord> records)
        {
            _log.Info($"[占位] 批量上传 {records.Count} 条记录");
            return await Task.FromResult(true);
        }

        public async Task<bool> TestConnectionAsync()
        {
            _log.Info("[占位] MES连接测试");
            return await Task.FromResult(_isConnected);
        }

        public async Task CloseAsync()
        {
            _isConnected = false;
            _log.Info("MES服务已关闭");
            await Task.CompletedTask;
        }
    }
}
