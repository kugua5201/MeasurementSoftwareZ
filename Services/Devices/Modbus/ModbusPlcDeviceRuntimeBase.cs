using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services.Devices.Modbus
{
    /// <summary>
    /// Modbus 设备运行时抽象基类。
    /// 用于承载 Modbus TCP 和 Modbus RTU 的共同行为。
    /// </summary>
    public abstract class ModbusPlcDeviceRuntimeBase : PlcDeviceRuntimeBase
    {
        protected ModbusPlcDeviceRuntimeBase(PlcDevice device) : base(device)
        {
        }
    }
}
