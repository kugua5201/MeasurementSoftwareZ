using MeasurementSoftware.Models;
using MultiProtocol.Model;
using MultiProtocol.Services.IIndustrialProtocol;
using MultiProtocol.Services.Siemens;
using DriveType = MultiProtocol.Model.DriveType;

namespace MeasurementSoftware.Services.Devices.Siemens
{
    /// <summary>
    /// 西门子 S7-1500 设备运行时。
    /// </summary>
    public sealed class SiemensS1500PlcDeviceRuntime : SiemensPlcDeviceRuntimeBase
    {
        public SiemensS1500PlcDeviceRuntime(PlcDevice device) : base(device)
        {
        }

        protected override IIndustrialProtocol CreateProtocol(ConnectionArgs args)
        {
            return new SiemensS1500PLC(args);
        }

        protected override DriveType GetSiemensDriveType()
        {
            return DriveType.SiemensS7_1500;
        }
    }
}
