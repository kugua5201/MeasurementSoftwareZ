using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services.Devices
{
    /// <summary>
    /// PLC 运行时工厂。
    /// 根据设备类型创建对应的运行时实现。
    /// </summary>
    public interface IPlcDeviceRuntimeFactory
    {
        /// <summary>
        /// 为指定设备创建运行时实例。
        /// </summary>
        /// <param name="device">设备模型。</param>
        /// <returns>与设备类型匹配的运行时实例。</returns>
        IPlcDeviceRuntime CreateRuntime(PlcDevice device);
    }
}
