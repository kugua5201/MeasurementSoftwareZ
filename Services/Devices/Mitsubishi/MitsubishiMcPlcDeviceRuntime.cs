using MeasurementSoftware.Models;
using MultiProtocol.Model;
using MultiProtocol.Services.IIndustrialProtocol;
using MultiProtocol.Services.Mitsubishi;
using DriveType = MultiProtocol.Model.DriveType;

namespace MeasurementSoftware.Services.Devices.Mitsubishi
{
    /// <summary>
    /// 三菱 MC 设备运行时。
    /// 当前仅预留结构，后续可在此补充具体协议实现。
    /// </summary>
    public sealed class MitsubishiMcPlcDeviceRuntime : PlcDeviceRuntimeBase
    {
        public MitsubishiMcPlcDeviceRuntime(PlcDevice device) : base(device)
        {
        }

        protected override IIndustrialProtocol? CreateProtocol(ConnectionArgs args)
        {
            return new MitsubishiMcBinaryPLC(args);
        }

        protected override DriveType GetDriveType()
        {
            return DriveType.MitsubishiMcBinary;
        }
    }
}
