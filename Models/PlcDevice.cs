using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.ViewModels;
using System.Collections.ObjectModel;

namespace MeasurementSoftware.Models
{
    /// <summary>
    /// PLC设备类型
    /// </summary>
    public enum PlcDeviceType
    {
        SiemensS7,      // 西门子S7
        MitsubishiMC,   // 三菱MC
        ModbusTCP,      // Modbus-TCP
        ModbusRTU       // Modbus-RTU
    }

    /// <summary>
    /// PLC设备模型
    /// </summary>
    public partial class PlcDevice : ObservableViewModel
    {
        /// <summary>
        /// 设备ID
        /// </summary>
        [ObservableProperty]
        private string deviceId = string.Empty;

        /// <summary>
        /// 设备名称
        /// </summary>
        [ObservableProperty]
        private string deviceName = string.Empty;

        /// <summary>
        /// 设备类型
        /// </summary>
        [ObservableProperty]
        private PlcDeviceType deviceType;

        /// <summary>
        /// IP地址（用于TCP连接）
        /// </summary>
        [ObservableProperty]
        private string ipAddress = "192.168.0.1";

        /// <summary>
        /// 端口号（用于TCP连接）
        /// </summary>
        [ObservableProperty]
        private int port = 502;

        /// <summary>
        /// 从站地址（用于Modbus）
        /// </summary>
        [ObservableProperty]
        private byte slaveAddress = 1;

        /// <summary>
        /// 机架号（用于西门子S7）
        /// </summary>
        [ObservableProperty]
        private int rack = 0;

        /// <summary>
        /// 槽号（用于西门子S7）
        /// </summary>
        [ObservableProperty]
        private int slot = 0;

        /// <summary>
        /// COM口（用于RTU连接）
        /// </summary>
        [ObservableProperty]
        private string comPort = "COM1";

        /// <summary>
        /// 波特率
        /// </summary>
        [ObservableProperty]
        private int baudRate = 9600;

        /// <summary>
        /// 数据位
        /// </summary>
        [ObservableProperty]
        private int dataBits = 8;

        /// <summary>
        /// 停止位
        /// </summary>
        [ObservableProperty]
        private double stopBits = 1;

        /// <summary>
        /// 校验位
        /// </summary>
        [ObservableProperty]
        private string parity = "None";

        /// <summary>
        /// 连接超时（毫秒）
        /// </summary>
        [ObservableProperty]
        private int connectionTimeout = 5000;

        /// <summary>
        /// 接收超时（毫秒）
        /// </summary>
        [ObservableProperty]
        private int receiveTimeout = 5000;

        /// <summary>
        /// 轮询周期（毫秒）
        /// </summary>
        [ObservableProperty]
        private int pollingSpeed = 1000;

        /// <summary>
        /// 是否启用
        /// </summary>
        [ObservableProperty]
        private bool isEnabled = true;

        /// <summary>
        /// 连接状态
        /// </summary>
        [ObservableProperty]
        private bool isConnected;

        /// <summary>
        /// 数据点列表
        /// </summary>
        public ObservableCollection<DataPoint> DataPoints { get; set; } = new();
    }
}
