using CommunityToolkit.Mvvm.ComponentModel;
using HandyControl.Controls;
using HandyControl.Data;
using MeasurementSoftware.Extensions;
using MeasurementSoftware.Services.Config;
using MeasurementSoftware.Services.Events;
using MeasurementSoftware.ViewModels;
using MultiProtocol.Model;
using MultiProtocol.Services.IIndustrialProtocol;
using MultiProtocol.Services.Modbus;
using MultiProtocol.Services.Siemens;
using MultiProtocol.Utils;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using DriveType = MultiProtocol.Model.DriveType;
using MessageBox = HandyControl.Controls.MessageBox;

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
        /// 地址从0开始
        /// </summary>
        [ObservableProperty]
        private bool addressStartWithZero = false;

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
            set
            {
                if (!value)
                {
                    var recipeService = ContainerBuilderExtensions.GetService<IRecipeConfigService>();
                    var currentRecipe = recipeService?.CurrentRecipe;
                    if (currentRecipe != null)
                    {
                        bool hasBind = currentRecipe.Channels.Any(c => c.PlcDeviceId == DeviceId);
                        if (hasBind)
                        {
                            var res = MessageBox.Show("当前设备已被通道绑定，强制关闭可能导致测量异常，是否继续？", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                            if (res == MessageBoxResult.No)
                            {
                                return; // 不赋值，不触发 UI
                            }
                            else
                            {
                                foreach (var channel in currentRecipe.Channels.Where(c => c.PlcDeviceId == DeviceId))
                                {
                                    channel.PlcDeviceId = 0;
                                    channel.DataPointId = string.Empty;
                                    channel.DataSourceAddress = string.Empty;
                                    channel.AvailableDataPoints = new ObservableCollection<DataPoint>();
                                }
                            }
                        }
                    }
                }
                SetProperty(ref isEnabled, value, () => protocol?.Open(value));

            }
        }


        /// <summary>
        /// 是否已连接
        /// </summary>
        [ObservableProperty]
        [property: System.Text.Json.Serialization.JsonIgnore]
        private bool isConnected;

        /// <summary>
        /// 数据点列表
        /// </summary>
        public ObservableCollection<DataPoint> DataPoints { get; set; } = [];

        [System.Text.Json.Serialization.JsonIgnore]
        public IIndustrialProtocol protocol { get; private set; }

        /// <summary>
        /// 协议数据读取事件处理：将读取的FieldInfo数据映射回DataPoint
        /// </summary>
        private void Protocol_OnDataRead(object? sender, MultiProtocol.Model.DataEventArgs e)
        {
            foreach (var fieldInfo in e.Data)
            {
                // 通过地址匹配到对应的DataPoint
                var dataPoint = DataPoints.FirstOrDefault(dp => dp.Address == fieldInfo.Address);
                if (dataPoint == null) continue;

                dataPoint.IsSuccess = fieldInfo.IsSuccess;
                dataPoint.LastUpdateTime = fieldInfo.Time == default ? DateTime.Now : fieldInfo.Time;

                if (fieldInfo.IsSuccess)
                {
                    dataPoint.CurrentValue = fieldInfo.Value;
                    dataPoint.ErrorMessage = null;

                }
                else
                {
                    dataPoint.ErrorMessage = fieldInfo.Message;
                }
            }
        }

        /// <summary>
        /// 连接状态变化事件处理
        /// </summary>
        private void Protocol_OnConnectChanged(object? sender, bool connected)
        {
            IsConnected = connected;
            if (IsConnected)
            {
                SetDevicePoints();
                protocol.Open(IsEnabled);
            }
        }

        /// <summary>
        /// 订阅协议事件
        /// </summary>
        private void SubscribeProtocolEvents()
        {
            protocol?.OnDataRead += Protocol_OnDataRead;
            protocol?.OnConnectChanged += Protocol_OnConnectChanged;
        }

        /// <summary>
        /// 取消订阅协议事件
        /// </summary>
        private void UnsubscribeProtocolEvents()
        {
            protocol?.OnDataRead -= Protocol_OnDataRead;
            protocol?.OnConnectChanged -= Protocol_OnConnectChanged;
        }

        /// <summary>
        /// 构建ConnectionArgs（提取公共逻辑）
        /// </summary>
        private ConnectionArgs BuildConnectionArgs()
        {
            return new ConnectionArgs
            {
                IpAddress = this.IpAddress,
                Port = this.Port,
                PortName = this.ComPort,
                BaudRate = (int)this.BaudRate,
                DataBit = (int)this.DataBits,
                RtsEnable = this.RtsEnable,
                DtrEnable = this.DtrEnable,
                Slot = (byte)this.Slot,
                AutoReconnect = this.AutoReconnect,
                ReconnectInterval = this.ReconnectInterval,
                ConnectionTimeout = this.ConnectionTimeout,
                ReceiveTimeOut = this.ReceiveTimeout,
                OperationTimeout = this.OperationTimeout,
                PollingSpeed = this.PollingSpeed,
                MaxErrorConnections = this.MaxErrorConnections,
                MaxConnections = this.MaxConnections,
                AddressStartWithZero = this.AddressStartWithZero,
            };
        }

        /// <summary>
        /// 根据设备类型创建协议实例（不含SetDevice）
        /// </summary>
        private void CreateProtocolInstance(ConnectionArgs args)
        {
            switch (DeviceType)
            {
                case PlcDeviceType.ModbusTCP:
                    protocol = new ModbusTcpPLC(args);
                    break;
                case PlcDeviceType.ModbusRTU:
                    protocol = new ModbusRtuPLC(args);
                    break;
                case PlcDeviceType.SiemensS7_1200:
                    protocol = new SiemensS1200PLC(args);
                    break;
                case PlcDeviceType.SiemensS7_1500:
                    protocol = new SiemensS1500PLC(args);
                    break;
                case PlcDeviceType.MitsubishiMC:
                    // TODO: 实现 MitsubishiMC
                    break;
            }
        }

        /// <summary>
        /// 将当前点位列表注册到协议实例
        /// </summary>
        private void SetDevicePoints()
        {
            if (protocol == null || DataPoints.Count == 0) return;

            //protocol.RemoveDevice(DeviceId);

            DeviceInfo device = new(DeviceId, DeviceName);
            List<FieldInfo> fieldInfos = [];
            foreach (DataPoint dataPoint in DataPoints)
            {
                FieldInfo info = new(dataPoint.Address, dataPoint.DataType, dataPoint.ByteOrder);
                fieldInfos.Add(info);
            }

            var driveType = DeviceType switch
            {
                PlcDeviceType.ModbusTCP => DriveType.ModbusTcpNet,
                PlcDeviceType.ModbusRTU => DriveType.ModbusRtu,
                PlcDeviceType.SiemensS7_1200 => DriveType.SiemensS7_1200,
                PlcDeviceType.SiemensS7_1500 => DriveType.SiemensS7_1500,
                PlcDeviceType.MitsubishiMC => DriveType.MitsubishiMcBinary,
                _ => DriveType.ModbusTcpNet
            };

            var checkedFields = DataFieldsHelper.CheckFileds(driveType, fieldInfos);
            protocol.SetDevice(device, checkedFields);
        }

        /// <summary>
        /// 重新将最新点位列表绑定到协议实例（外部调用，如保存点位后）
        /// </summary>
        public void ResetDevicePoints()
        {
            if (protocol == null) return;
            SetDevicePoints();
        }

        /// <summary>
        /// 销毁旧的协议实例，释放连接资源
        /// </summary>
        public async Task DestroyPlcAsync()
        {
            if (protocol != null)
            {
                try
                {
                    UnsubscribeProtocolEvents();
                    if (protocol.IsOpen)
                    {
                        await protocol.DisconnectAsync();
                    }
                    protocol.Dispose();
                }
                catch { }
                protocol = null!;
                IsConnected = false;
            }
        }

        /// <summary>
        /// 初始化PLC协议实例（会先销毁旧实例，不含连接）
        /// </summary>
        public async Task<IIndustrialProtocol> InitPlcAsync()
        {
            await DestroyPlcAsync();

            var args = BuildConnectionArgs();
            CreateProtocolInstance(args);

            if (protocol != null)
            {
                SubscribeProtocolEvents();
            }

            return protocol;
        }

        /// <summary>
        /// 同步版本的初始化（兼容旧调用）
        /// </summary>
        public IIndustrialProtocol InitPlc()
        {
            if (protocol != null)
            {
                try
                {
                    UnsubscribeProtocolEvents();
                    if (protocol.IsOpen)
                        protocol.Disconnect();
                    protocol.Dispose();
                }
                catch { }
                protocol = null!;
                IsConnected = false;
            }

            var args = BuildConnectionArgs();
            CreateProtocolInstance(args);

            if (protocol != null)
            {
                SubscribeProtocolEvents();
            }

            return protocol;
        }

        /// <summary>
        /// 连接PLC并注册点位，启用时开始轮询
        /// </summary>
        public async Task<(bool Success, string Message)> ConnectAsync()
        {
            if (protocol == null)
                return (false, "协议实例未初始化，请先调用 InitPlc");

            try
            {
                var result = protocol.Connect();
                if (result.IsSuccess)
                {
                    IsConnected = true;

                    // 连接成功后注册点位，协议轮询线程会读取这些点位
                    SetDevicePoints();

                    // 如果设备已启用，开启轮询读取
                    if (IsEnabled)
                    {
                        protocol.Open(true);
                    }

                    return (true, "连接成功");
                }
                else
                {
                    IsConnected = false;
                    return (false, $"{result.Message}");
                }
            }
            catch (Exception ex)
            {
                IsConnected = false;
                return (false, $"连接异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 断开PLC连接
        /// </summary>
        public async Task DisconnectAsync()
        {
            if (protocol != null)
            {
                try
                {
                    protocol.Open(false);
                    await protocol.DisconnectAsync();
                }
                catch { }
                IsConnected = false;
            }
        }



    }
}
