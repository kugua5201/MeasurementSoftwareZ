using MeasurementSoftware.Models;
using MultiProtocol.Model;
using MultiProtocol.Services.IIndustrialProtocol;
using MultiProtocol.Services.Modbus;
using DriveType = MultiProtocol.Model.DriveType;

namespace MeasurementSoftware.Services.Devices.Modbus
{
    /// <summary>
    /// Modbus TCP 设备运行时。
    /// </summary>
    public sealed class ModbusTcpPlcDeviceRuntime : ModbusPlcDeviceRuntimeBase
    {
        public ModbusTcpPlcDeviceRuntime(PlcDevice device) : base(device)
        {
        }

        protected override IIndustrialProtocol CreateProtocol(ConnectionArgs args)
        {
            return new ModbusTcpPLC(args);
        }

        protected override DriveType GetDriveType()
        {
            return DriveType.ModbusTcpNet;
        }
    }
}
