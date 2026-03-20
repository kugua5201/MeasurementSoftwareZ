using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Devices.Mitsubishi;
using MeasurementSoftware.Services.Devices.Modbus;
using MeasurementSoftware.Services.Devices.Siemens;

namespace MeasurementSoftware.Services.Devices
{
    /// <summary>
    /// PLC 运行时工厂实现。
    /// 按设备类型返回对应的独立运行时对象。
    /// </summary>
    public class PlcDeviceRuntimeFactory : IPlcDeviceRuntimeFactory
    {
        /// <inheritdoc />
        public IPlcDeviceRuntime CreateRuntime(PlcDevice device)
        {
            ArgumentNullException.ThrowIfNull(device);

            return device.DeviceType switch
            {
                PlcDeviceType.ModbusTCP => new ModbusTcpPlcDeviceRuntime(device),
                PlcDeviceType.ModbusRTU => new ModbusRtuPlcDeviceRuntime(device),
                PlcDeviceType.SiemensS7_1200 => new SiemensS1200PlcDeviceRuntime(device),
                PlcDeviceType.SiemensS7_1500 => new SiemensS1500PlcDeviceRuntime(device),
                PlcDeviceType.MitsubishiMC => new MitsubishiMcPlcDeviceRuntime(device),
                _ => throw new NotSupportedException($"不支持的设备类型: {device.DeviceType}")
            };
        }
    }
}
