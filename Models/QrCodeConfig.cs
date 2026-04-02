using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.ViewModels;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace MeasurementSoftware.Models
{


    /// <summary>
    /// 二维码配置
    /// </summary>
    public partial class QrCodeConfig : ObservableViewModel
    {
        public QrCodeConfig()
        {
            // 监听选中设备和点位的变化，自动更新ID
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SelectedPlcDevice))
                {
                    if (SelectedPlcDevice != null)
                    {
                        PlcDeviceId = SelectedPlcDevice.DeviceId;
                        // 切换设备时清空选中的点位
                        SelectedPoint = null;
                    }

                }
                else if (e.PropertyName == nameof(SelectedPoint))
                {
                    if (SelectedPoint != null)
                    {
                        Address = SelectedPoint.PointId;
                    }
                }
            };
        }

        /// <summary>
        /// 是否启用二维码（不启用则使用批次流水号）
        /// </summary>
        [ObservableProperty]
        private bool isEnabled = true;

        /// <summary>
        /// 是否要求启动测量后必须先完成扫码。
        /// 启用后会先清空本轮数据并等待新的扫码结果，收到有效二维码后才开始通道采集。
        /// </summary>
        private bool requireQrCodeBeforeMeasurement;

        public bool RequireQrCodeBeforeMeasurement
        {
            get => requireQrCodeBeforeMeasurement;
            set => SetProperty(ref requireQrCodeBeforeMeasurement, value);
        }

        /// <summary>
        /// 数据源类型
        /// </summary>
        [ObservableProperty]
        private QrCodeSourceType sourceType = QrCodeSourceType.KeyboardInput;

        #region 串口通信配置

        [ObservableProperty]
        private string serialPortName = "COM1";

        [ObservableProperty]
        private BaudRate baudRate = BaudRate.Baud9600;

        [ObservableProperty]
        private DataBits dataBits = DataBits.Eight;

        [ObservableProperty]
        private System.IO.Ports.Parity parity = System.IO.Ports.Parity.None;

        [ObservableProperty]
        private System.IO.Ports.StopBits stopBits = System.IO.Ports.StopBits.One;

        #endregion

        #region 以太网通信配置

        [ObservableProperty]
        private string ethernetIp = "192.168.1.100";

        [ObservableProperty]
        private int ethernetPort = 8080;

        [ObservableProperty]
        private NetworkProtocol ethernetProtocol = NetworkProtocol.TCP;

        #endregion

        #region PLC寄存器配置

        [ObservableProperty]
        private long plcDeviceId = 0;

        [ObservableProperty]
        private int plcReadLength = 20;

        [ObservableProperty]
        private string address = string.Empty;

        /// <summary>
        /// 当前选中的PLC设备对象（运行时UI状态，不序列化）
        /// </summary>
        [ObservableProperty]
        [JsonIgnore]
        private PlcDevice? selectedPlcDevice;

        /// <summary>
        /// 当前选中的点位对象（运行时UI状态，不序列化）
        /// </summary>
        [ObservableProperty]
        [JsonIgnore]
        private DataPoint? selectedPoint;

        #endregion

        #region 数据校验配置

        /// <summary>
        /// 是否启用起始符校验
        /// </summary>
        [ObservableProperty]
        private bool enableStartSymbol;

        /// <summary>
        /// 起始符（支持HEX格式，如：0x02 或 STX）
        /// </summary>
        [ObservableProperty]
        private string startSymbol = string.Empty;

        /// <summary>
        /// 是否启用结束符校验
        /// </summary>
        [ObservableProperty]
        private bool enableEndSymbol;

        /// <summary>
        /// 结束符（支持HEX格式，如：0x03 或 ETX）
        /// </summary>
        [ObservableProperty]
        private string endSymbol = string.Empty;

        /// <summary>
        /// 是否启用长度校验
        /// </summary>
        [ObservableProperty]
        private bool enableLengthCheck;

        /// <summary>
        /// 期望的数据长度
        /// </summary>
        [ObservableProperty]
        private int expectedLength = 20;

        #endregion

        #region 数据提取配置

        /// <summary>
        /// 二维码起始索引（从第几位开始，0基）
        /// </summary>
        [ObservableProperty]
        private int qrCodeStartIndex = 0;

        /// <summary>
        /// 二维码提取长度
        /// </summary>
        [ObservableProperty]
        private int qrCodeLength = 20;

        #endregion

        #region 批次流水号配置（二维码未启用时使用）

        /// <summary>
        /// 流水号前缀
        /// </summary>
        [ObservableProperty]
        private string batchPrefix = "BATCH";

        /// <summary>
        /// 流水号日期格式（如：yyyyMMdd）
        /// </summary>
        [ObservableProperty]
        private string batchDateFormat = "yyyyMMdd";

        /// <summary>
        /// 流水号位数
        /// </summary>
        [ObservableProperty]
        private int batchSerialDigits = 4;

        /// <summary>
        /// 当前流水号计数器
        /// </summary>
        [ObservableProperty]
        private int currentBatchSerial = 1;

        /// <summary>
        /// 上次生成流水号的日期
        /// </summary>
        [ObservableProperty]
        private string lastBatchDate = string.Empty;

        #endregion

        #region 测试与状态

        /// <summary>
        /// 测试接收到的原始数据（运行时，不序列化）
        /// </summary>
        [ObservableProperty]
        [JsonIgnore]
        private string testRawData = string.Empty;

        /// <summary>
        /// 测试提取后的二维码（运行时，不序列化）
        /// </summary>
        [ObservableProperty]
        [JsonIgnore]
        private string testExtractedQrCode = string.Empty;

        /// <summary>
        /// 测试验证结果（运行时，不序列化）
        /// </summary>
        [ObservableProperty]
        [JsonIgnore]
        private string testValidationResult = string.Empty;

        /// <summary>
        /// 最近一次二维码校验展示文本（运行时，不序列化）。
        /// 成功时显示提取结果，失败时显示错误原因。
        /// </summary>
        [ObservableProperty]
        [JsonIgnore]
        private string runtimeDisplayText = "未扫码";

        /// <summary>
        /// 最近一次二维码校验详细结果（运行时，不序列化）。
        /// </summary>
        [ObservableProperty]
        [JsonIgnore]
        private string runtimeValidationMessage = string.Empty;

        /// <summary>
        /// 最近一次二维码校验是否通过（运行时，不序列化）。
        /// null 表示尚未开始或等待扫码中。
        /// </summary>
        [ObservableProperty]
        [JsonIgnore]
        private bool? runtimeValidationPassed;

        #endregion
    }
}
