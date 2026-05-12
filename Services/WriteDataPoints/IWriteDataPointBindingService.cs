using MeasurementSoftware.Models;
using System.Collections.ObjectModel;

namespace MeasurementSoftware.Services.WriteDataPoints
{
    /// <summary>
    /// 写入点位运行时绑定服务。
    /// 负责维护设备集合监听、运行时设备/点位引用以及可选点位列表。
    /// </summary>
    public interface IWriteDataPointBindingService
    {
        /// <summary>
        /// 绑定可选设备集合，并按当前配置同步运行时引用。
        /// </summary>
        void AttachAvailableDevices(WriteDataPointConfig config, ObservableCollection<PlcDevice>? devices);

        /// <summary>
        /// 解除可选设备集合绑定，并清空运行时引用。
        /// </summary>
        void DetachAvailableDevices(WriteDataPointConfig config);

        /// <summary>
        /// 按已保存的设备/点位标识回填运行时绑定。
        /// </summary>
        void HydrateRuntimeBindings(WriteDataPointConfig config, PlcDevice? device);

        /// <summary>
        /// 直接绑定运行时设备实例。
        /// </summary>
        void BindRuntimeDevice(WriteDataPointConfig config, PlcDevice? device);

        /// <summary>
        /// 直接绑定运行时点位实例。
        /// </summary>
        void BindRuntimeDataPoint(WriteDataPointConfig config, DataPoint? dataPoint);
    }
}
