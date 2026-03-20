using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services.Devices
{
    /// <summary>
    /// 单个 PLC 设备的运行时实例接口。
    /// 每个实现只负责一种协议族或具体协议类型的运行时行为。
    /// </summary>
    public interface IPlcDeviceRuntime
    {
        /// <summary>
        /// 当前运行时对应的设备模型。
        /// </summary>
        PlcDevice Device { get; }

        /// <summary>
        /// 初始化协议实例并挂接事件。
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// 建立设备连接。
        /// </summary>
        Task<(bool Success, string Message)> ConnectAsync();

        /// <summary>
        /// 断开设备连接。
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// 销毁协议实例并释放资源。
        /// </summary>
        Task DestroyAsync();

        /// <summary>
        /// 将当前设备点位重新下发到协议层。
        /// </summary>
        void ResetDevicePoints();

        /// <summary>
        /// 设置协议轮询启停。
        /// </summary>
        /// <param name="enabled">true 表示启用轮询；false 表示停止轮询。</param>
        void SetPollingEnabled(bool enabled);

        /// <summary>
        /// 主动读取单个点位的值。
        /// </summary>
        Task<object?> ReadDataPointValueAsync(DataPoint dataPoint);

        /// <summary>
        /// 向单个点位写入值。
        /// </summary>
        Task<(bool Success, string? Message)> WriteDataPointValueAsync(DataPoint dataPoint, object value);
    }
}
