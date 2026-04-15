using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Config;
using MeasurementSoftware.Services.Devices;

namespace MeasurementSoftware.Services.Measurements
{
    /// <summary>
    /// 测量通道模式处理器。
    /// 按通道模式分别封装配置、校验与采集逻辑。
    /// </summary>
    public interface IMeasurementChannelHandler
    {
        /// <summary>
        /// 当前处理器支持的通道模式。
        /// </summary>
        MeasurementChannelMode Mode { get; }

        /// <summary>
        /// 初始化新建通道的默认配置。
        /// </summary>
        void InitializeNewChannel(MeasurementChannel channel, IReadOnlyList<PlcDevice> enabledDevices);

        /// <summary>
        /// 按已保存配置回填运行时绑定。
        /// </summary>
        void HydrateBindings(MeasurementChannel channel, IDeviceConfigService deviceConfigService);

        /// <summary>
        /// 将编辑态绑定同步回持久化字段。
        /// </summary>
        void SyncBindings(MeasurementChannel channel, IDeviceConfigService deviceConfigService);

        /// <summary>
        /// 校验当前通道配置是否合法。
        /// </summary>
        bool ValidateConfiguration(MeasurementChannel channel, out string errorMessage);

        /// <summary>
        /// 处理普通点位更新。
        /// </summary>
        bool TryHandleDataPointUpdates(MeasurementChannel channel, PlcDataPointsUpdatedEventArgs e);

        /// <summary>
        /// 处理缓存字段更新。
        /// </summary>
        bool TryHandleCacheFieldUpdates(MeasurementChannel channel, PlcCacheFieldsUpdatedEventArgs e);

        /// <summary>
        /// 处理设备连接状态变化。
        /// </summary>
        bool TryHandleConnectionStateChanged(MeasurementChannel channel, PlcDeviceConnectionChangedEventArgs e);

        /// <summary>
        /// 重置当前通道在处理器中的运行期状态。
        /// 用于开始测量、停止测量、终止测量或清空数据时清理处理器内部缓存。
        /// </summary>
        void ResetRuntimeState(MeasurementChannel channel);
    }
}
