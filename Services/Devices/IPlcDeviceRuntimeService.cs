using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services.Devices
{
    /// <summary>
    /// PLC 设备运行时管理服务。
    /// 负责为每个设备选择并管理对应的运行时实例。
    /// </summary>
    public interface IPlcDeviceRuntimeService
    {
        /// <summary>
        /// 为指定设备创建并初始化匹配的运行时实例。
        /// </summary>
        Task InitializeAsync(PlcDevice device);

        /// <summary>
        /// 连接指定设备。
        /// </summary>
        Task<(bool Success, string Message)> ConnectAsync(PlcDevice device);

        /// <summary>
        /// 断开指定设备连接。
        /// </summary>
        Task DisconnectAsync(PlcDevice device);

        /// <summary>
        /// 销毁指定设备的运行时实例。
        /// </summary>
        Task DestroyAsync(PlcDevice device);

        /// <summary>
        /// 将设备点位重新下发到运行时协议层。
        /// </summary>
        void ResetDevicePoints(PlcDevice device);

        /// <summary>
        /// 设置设备轮询启停。
        /// </summary>
        void SetPollingEnabled(PlcDevice device, bool enabled);

        /// <summary>
        /// 启动设备缓存读取。
        /// 仅对支持缓存能力的运行时生效。
        /// </summary>
        void StartCacheReading(PlcDevice device);

        /// <summary>
        /// 停止设备缓存读取。
        /// 仅对支持缓存能力的运行时生效。
        /// </summary>
        void StopCacheReading(PlcDevice device);

        /// <summary>
        /// 获取设备缓存字段值。
        /// 仅对支持缓存能力的运行时生效。
        /// </summary>
        double? GetCacheFieldValue(PlcDevice device, string cacheFieldId);

        /// <summary>
        /// 取出并清空设备缓存字段的待处理历史值。
        /// 仅对支持缓存能力的运行时生效。
        /// </summary>
        IReadOnlyList<double> TakeCacheFieldValues(PlcDevice device, string cacheFieldId);

        /// <summary>
        /// 主动读取指定设备点位的值。
        /// </summary>
        Task<object?> ReadDataPointValueAsync(PlcDevice device, DataPoint dataPoint);

        /// <summary>
        /// 向指定设备点位写入值。
        /// </summary>
        Task<(bool Success, string? Message)> WriteDataPointValueAsync(PlcDevice device, DataPoint dataPoint, object value);
    }
}
