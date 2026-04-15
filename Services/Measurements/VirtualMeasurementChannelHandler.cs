using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Config;
using MeasurementSoftware.Services.Devices;

namespace MeasurementSoftware.Services.Measurements
{
    /// <summary>
    /// 虚拟通道处理器。
    /// 当前仅保留展示与占位，不参与采集。
    /// </summary>
    public sealed class VirtualMeasurementChannelHandler : MeasurementChannelHandlerBase
    {
        public override MeasurementChannelMode Mode => MeasurementChannelMode.Virtual;

        public override void InitializeNewChannel(MeasurementChannel channel, IReadOnlyList<PlcDevice> enabledDevices)
        {
            channel.ChannelDescription = "虚拟通道暂未实现";
        }

        public override void HydrateBindings(MeasurementChannel channel, IDeviceConfigService deviceConfigService)
        {
            channel.ChannelDescription = "虚拟通道暂未实现";
        }

        public override void SyncBindings(MeasurementChannel channel, IDeviceConfigService deviceConfigService)
        {
            channel.ChannelDescription = "虚拟通道暂未实现";
        }

        public override bool ValidateConfiguration(MeasurementChannel channel, out string errorMessage)
        {
            errorMessage = string.Empty;
            return true;
        }

        public override bool TryHandleDataPointUpdates(MeasurementChannel channel, PlcDataPointsUpdatedEventArgs e)
        {
            return false;
        }

        public override bool TryHandleCacheFieldUpdates(MeasurementChannel channel, PlcCacheFieldsUpdatedEventArgs e)
        {
            return false;
        }

        public override bool TryHandleConnectionStateChanged(MeasurementChannel channel, PlcDeviceConnectionChangedEventArgs e)
        {
            return false;
        }
    }
}
