using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Config;
using MeasurementSoftware.Services.Devices;

namespace MeasurementSoftware.Services.Measurements
{
    /// <summary>
    /// 直接测量处理器。
    /// </summary>
    public sealed class DirectMeasurementChannelHandler : MeasurementChannelHandlerBase
    {
        public override MeasurementChannelMode Mode => MeasurementChannelMode.Direct;

        public override void InitializeNewChannel(MeasurementChannel channel, IReadOnlyList<PlcDevice> enabledDevices)
        {
            if (channel.RuntimeDevice == null)
            {
                channel.BindDevice(enabledDevices.FirstOrDefault());
            }
        }

        public override void HydrateBindings(MeasurementChannel channel, IDeviceConfigService deviceConfigService)
        {
            if (channel.PlcDeviceId == 0)
            {
                channel.ClearRuntimeBindings();
                return;
            }

            var device = FindDevice(deviceConfigService.Devices, channel.PlcDeviceId);
            if (device?.IsEnabled != true)
            {
                channel.ClearRuntimeBindings();
                return;
            }

            channel.HydrateRuntimeBindings(device);
        }

        public override void SyncBindings(MeasurementChannel channel, IDeviceConfigService deviceConfigService)
        {
            if (channel.RuntimeDevice != null)
            {
                channel.PlcDeviceId = channel.RuntimeDevice.DeviceId;
            }

            if (channel.RuntimeDataPoint != null)
            {
                channel.DataPointId = channel.RuntimeDataPoint.PointId;
                channel.DataSourceAddress = channel.RuntimeDataPoint.Address;
            }
            else if (channel.PlcDeviceId == 0)
            {
                channel.DataPointId = string.Empty;
                channel.DataSourceAddress = string.Empty;
                channel.UseCacheValue = false;
            }

            if (channel.PlcDeviceId != 0)
            {
                HydrateBindings(channel, deviceConfigService);
            }
        }

        public override bool ValidateConfiguration(MeasurementChannel channel, out string errorMessage)
        {
            if (channel.RuntimeDevice == null || channel.RuntimeDataPoint == null)
            {
                errorMessage = "直接测量必须绑定一个设备和数据点位";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        public override bool TryHandleDataPointUpdates(MeasurementChannel channel, PlcDataPointsUpdatedEventArgs e)
        {
            if (channel.UseCacheValue || !ReferenceEquals(channel.RuntimeDevice, e.Device))
            {
                return false;
            }

            var dataPoint = channel.RuntimeDataPoint;
            if (dataPoint == null || !e.DataPoints.Contains(dataPoint))
            {
                return false;
            }

            if (!TryGetChannelCurrentValue(channel, dataPoint, out var rawValue))
            {
                return false;
            }

            channel.UpdateMeasuredValue(rawValue);
            channel.DisplayState = MeasurementResult.Acquiring;
            channel.ChannelDescription = string.Empty;
            return true;
        }

        public override bool TryHandleCacheFieldUpdates(MeasurementChannel channel, PlcCacheFieldsUpdatedEventArgs e)
        {
            if (!channel.UseCacheValue || !ReferenceEquals(channel.RuntimeDevice, e.Device))
            {
                return false;
            }

            var dataPoint = channel.RuntimeDataPoint;
            var cacheFieldKey = dataPoint?.CacheFieldKey;
            if (dataPoint == null || string.IsNullOrWhiteSpace(cacheFieldKey))
            {
                return false;
            }

            var update = e.Updates.FirstOrDefault(u => string.Equals(u.CacheFieldKey, cacheFieldKey, StringComparison.OrdinalIgnoreCase));
            if (update == null)
            {
                return false;
            }

            if (!TryGetChannelCurrentValue(channel, dataPoint, out var rawValue))
            {
                channel.ChannelDescription = update.ErrorMessage ?? channel.ChannelDescription;
                return false;
            }

            if (update.NumericValues.Count <= 0)
            {
                return false;
            }

            channel.AppendMeasuredCacheSiemensValues(update.NumericValues, rawValue);
            channel.DisplayState = MeasurementResult.Acquiring;
            channel.ChannelDescription = string.Empty;
            return true;
        }

        public override bool TryHandleConnectionStateChanged(MeasurementChannel channel, PlcDeviceConnectionChangedEventArgs e)
        {
            if (!ReferenceEquals(channel.RuntimeDevice, e.Device))
            {
                return false;
            }

            if (!e.IsConnected)
            {
                channel.ChannelDescription = $"设备 {e.Device.DeviceName} 未连接";
                channel.DisplayState = MeasurementResult.Waiting;
                return true;
            }

            if (channel.RuntimeDataPoint?.IsSuccess == true && channel.RuntimeDataPoint.CurrentValue != null)
            {
                channel.ChannelDescription = string.Empty;
                channel.DisplayState = MeasurementResult.Acquiring;
            }
            else
            {
                channel.ChannelDescription = "设备已重连，等待数据更新...";
                channel.DisplayState = MeasurementResult.Acquiring;
            }

            return true;
        }
    }
}
