using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.ViewModels;

namespace MeasurementSoftware.Models
{
    /// <summary>
    /// 二维码绑定时机
    /// </summary>
    public enum BindingTiming
    {
        /// <summary>
        /// 测量开始时绑定
        /// </summary>
        BeforeMeasurement,

        /// <summary>
        /// 测量结束时绑定
        /// </summary>
        AfterMeasurement
    }

    /// <summary>
    /// 二维码扫描器类型
    /// </summary>
    public enum ScannerType
    {
        /// <summary>
        /// 串口扫码枪
        /// </summary>
        SerialScanner,

        /// <summary>
        /// TCP扫码枪
        /// </summary>
        TcpScanner,

        /// <summary>
        /// 从PLC读取
        /// </summary>
        PlcSource
    }

    /// <summary>
    /// 二维码绑定配置
    /// </summary>
    public partial class BarcodeBindingConfig : ObservableViewModel
    {
        /// <summary>
        /// 是否启用二维码绑定
        /// </summary>
        [ObservableProperty]
        private bool isEnabled;

        /// <summary>
        /// 绑定时机
        /// </summary>
        [ObservableProperty]
        private BindingTiming bindingTiming = BindingTiming.BeforeMeasurement;

        /// <summary>
        /// 扫描器类型
        /// </summary>
        [ObservableProperty]
        private ScannerType scannerType = ScannerType.SerialScanner;

        /// <summary>
        /// 串口配置（用于SerialScanner）
        /// </summary>
        [ObservableProperty]
        private string comPort = "COM1";

        /// <summary>
        /// 波特率
        /// </summary>
        [ObservableProperty]
        private int baudRate = 9600;

        /// <summary>
        /// TCP配置（用于TcpScanner）
        /// </summary>
        [ObservableProperty]
        private string tcpAddress = "192.168.1.100";

        /// <summary>
        /// TCP端口
        /// </summary>
        [ObservableProperty]
        private int tcpPort = 5000;

        /// <summary>
        /// PLC设备ID（用于PlcSource，0表示未关联）
        /// </summary>
        [ObservableProperty]
        private long plcDeviceId;

        /// <summary>
        /// PLC地址（用于PlcSource）
        /// </summary>
        [ObservableProperty]
        private string plcAddress = string.Empty;

        /// <summary>
        /// 二维码长度验证
        /// </summary>
        [ObservableProperty]
        private bool enableLengthValidation;

        /// <summary>
        /// 最小长度
        /// </summary>
        [ObservableProperty]
        private int minLength = 8;

        /// <summary>
        /// 最大长度
        /// </summary>
        [ObservableProperty]
        private int maxLength = 32;

        /// <summary>
        /// 二维码格式规则（正则表达式）
        /// </summary>
        [ObservableProperty]
        private bool enableFormatValidation;

        /// <summary>
        /// 格式规则（正则表达式）
        /// </summary>
        [ObservableProperty]
        private string formatPattern = @"^[A-Z0-9]+$";

        /// <summary>
        /// 是否允许重复扫描
        /// </summary>
        [ObservableProperty]
        private bool allowDuplicates;

        /// <summary>
        /// 扫描超时时间（秒）
        /// </summary>
        [ObservableProperty]
        private int scanTimeout = 30;
    }
}
