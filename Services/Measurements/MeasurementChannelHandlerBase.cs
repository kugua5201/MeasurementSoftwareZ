using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Config;
using MeasurementSoftware.Services.Devices;

namespace MeasurementSoftware.Services.Measurements
{
    /// <summary>
    /// 测量通道模式处理器基类。
    /// </summary>
    public abstract class MeasurementChannelHandlerBase : IMeasurementChannelHandler
    {
        public abstract MeasurementChannelMode Mode { get; }

        public abstract void InitializeNewChannel(MeasurementChannel channel, IReadOnlyList<PlcDevice> enabledDevices);

        public abstract void HydrateBindings(MeasurementChannel channel, IDeviceConfigService deviceConfigService);

        public abstract void SyncBindings(MeasurementChannel channel, IDeviceConfigService deviceConfigService);

        public abstract bool ValidateConfiguration(MeasurementChannel channel, out string errorMessage);

        public abstract bool TryHandleDataPointUpdates(MeasurementChannel channel, PlcDataPointsUpdatedEventArgs e);

        public abstract bool TryHandleCacheFieldUpdates(MeasurementChannel channel, PlcCacheFieldsUpdatedEventArgs e);

        public abstract bool TryHandleConnectionStateChanged(MeasurementChannel channel, PlcDeviceConnectionChangedEventArgs e);

        public virtual void ResetRuntimeState(MeasurementChannel channel)
        {
        }

        protected static PlcDevice? FindDevice(IReadOnlyCollection<PlcDevice> devices, long deviceId)
        {
            return devices.FirstOrDefault(d => d.DeviceId == deviceId);
        }

        protected static bool TryGetChannelCurrentValue(MeasurementChannel channel, DataPoint dataPoint, out double rawValue)
        {
            rawValue = default;
            var device = channel.RuntimeDevice;

            if (device == null)
            {
                channel.ChannelDescription = "未绑定设备或点位";
                return false;
            }

            var success = TryGetDataPointCurrentValue(device, dataPoint, out rawValue, out var errorMessage);
            channel.ChannelDescription = success ? string.Empty : errorMessage;
            return success;
        }

        protected static bool TryGetBindingCurrentValue(MeasurementChannel channel, MeasurementChannelSourceBinding binding, out double rawValue)
        {
            rawValue = default;
            var device = binding.RuntimeDevice;
            var dataPoint = binding.RuntimeDataPoint;
            if (device == null || dataPoint == null)
            {
                channel.ChannelDescription = $"变量 {binding.SourceKey} 未绑定设备或点位";
                return false;
            }

            if (!TryGetDataPointCurrentValue(device, dataPoint, out rawValue, out var errorMessage))
            {
                channel.ChannelDescription = $"变量 {binding.SourceKey}：{errorMessage}";
                return false;
            }

            return true;
        }

        protected static bool TryGetDataPointCurrentValue(PlcDevice device, DataPoint dataPoint, out double rawValue, out string errorMessage)
        {
            rawValue = default;
            errorMessage = string.Empty;

            if (!device.IsEnabled)
            {
                errorMessage = $"设备 {device.DeviceName} 未启用";
                return false;
            }

            if (!device.IsConnected)
            {
                errorMessage = $"设备 {device.DeviceName} 未连接";
                return false;
            }

            if (dataPoint.CurrentValue == null || !dataPoint.IsSuccess)
            {
                errorMessage = dataPoint.ErrorMessage ?? "读取中...";
                return false;
            }

            try
            {
                rawValue = Convert.ToDouble(dataPoint.CurrentValue);
                return true;
            }
            catch
            {
                errorMessage = "当前值无法转换为数值";
                return false;
            }
        }
    }
}
