using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.ViewModels;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace MeasurementSoftware.Models
{
    /// <summary>
    /// PLC设备模型
    /// </summary>
    public partial class PlcDevice : ObservableViewModel
    {
        #region 基本信息

        /// <summary>
        /// 设备ID
        /// </summary>
        [ObservableProperty]
        private long deviceId = 0;

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

        #endregion

        #region TCP/IP 参数

        /// <summary>
        /// IP地址
        /// </summary>
        [ObservableProperty]
        private string ipAddress = "192.168.0.1";

        /// <summary>
        /// 端口号
        /// </summary>
        [ObservableProperty]
        private ushort port = 502;

        #endregion

        #region 西门子S7 参数

        /// <summary>
        /// 机架号
        /// </summary>
        [ObservableProperty]
        private int rack = 0;

        /// <summary>
        /// 槽号
        /// </summary>
        [ObservableProperty]
        private int slot = 0;

        /// <summary>
        /// 西门子读取缓存配置
        /// </summary>
        [ObservableProperty]
        private SiemensReadCacheConfig siemensReadCache = new();

        #endregion

        #region Modbus 参数

        /// <summary>
        /// 从站地址
        /// </summary>
        [ObservableProperty]
        private byte slaveAddress = 1;


        /// <summary>
        /// 是否从0开始
        /// </summary>
        [ObservableProperty]
        private bool addressStartWithZero = false;


        #endregion

        #region 串口参数

        /// <summary>
        /// COM口
        /// </summary>
        [ObservableProperty]
        private string comPort = "COM1";

        /// <summary>
        /// 波特率
        /// </summary>
        [ObservableProperty]
        private BaudRate baudRate = BaudRate.Baud9600;

        /// <summary>
        /// 数据位
        /// </summary>
        [ObservableProperty]
        private DataBits dataBits = DataBits.Eight;

        /// <summary>
        /// 停止位
        /// </summary>
        [ObservableProperty]
        private System.IO.Ports.StopBits stopBits = System.IO.Ports.StopBits.One;

        /// <summary>
        /// 校验位
        /// </summary>
        [ObservableProperty]
        private System.IO.Ports.Parity parity = System.IO.Ports.Parity.None;

        /// <summary>
        /// RTS启用
        /// </summary>
        [ObservableProperty]
        private bool rtsEnable = false;

        /// <summary>
        /// DTR启用
        /// </summary>
        [ObservableProperty]
        private bool dtrEnable = false;

        #endregion

        #region 连接参数

        /// <summary>
        /// 自动重连
        /// </summary>
        [ObservableProperty]
        private bool autoReconnect = true;

        /// <summary>
        /// 重连间隔（毫秒）
        /// </summary>
        [ObservableProperty]
        private int reconnectInterval = 5000;

        /// <summary>
        /// 连接超时（毫秒）
        /// </summary>
        [ObservableProperty]
        private int connectionTimeout = 3000;

        /// <summary>
        /// 接收超时（毫秒）
        /// </summary>
        [ObservableProperty]
        private int receiveTimeout = 3000;

        /// <summary>
        /// 操作超时（毫秒）
        /// </summary>
        [ObservableProperty]
        private int operationTimeout = 3000;

        /// <summary>
        /// 轮询周期（毫秒）
        /// </summary>
        [ObservableProperty]
        private int pollingSpeed = 1000;

        /// <summary>
        /// 最大错误连接数
        /// </summary>
        [ObservableProperty]
        private int maxErrorConnections = 10;

        /// <summary>
        /// 最大连接数
        /// </summary>
        [ObservableProperty]
        private int maxConnections = 10;



        /// <summary>
        /// 编码格式
        /// </summary>
        [ObservableProperty]
        private string encoding = "UTF8";

        #endregion

        /// <summary>
        /// 是否启用
        /// </summary>
        private bool isEnabled = false;

        public bool IsEnabled
        {
            get => isEnabled;
            set => SetProperty(ref isEnabled, value);
        }


        /// <summary>
        /// 是否已连接
        /// </summary>
        [ObservableProperty]
        [JsonIgnore]
        private bool isConnected;

        /// <summary>
        /// 缓存是否正在读取
        /// </summary>
        [ObservableProperty]
        [JsonIgnore]
        private bool isCacheReading;

        /// <summary>
        /// 数据点列表
        /// </summary>
        public ObservableCollection<DataPoint> DataPoints { get; set; } = [];
    }
}
