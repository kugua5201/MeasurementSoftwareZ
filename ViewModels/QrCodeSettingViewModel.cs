using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Config;
using MeasurementSoftware.Services.Logs;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO.Ports;

namespace MeasurementSoftware.ViewModels
{
    public partial class QrCodeSettingViewModel : ObservableViewModel
    {
        private readonly ILog _log;
        private readonly IQrCodeConfigService _qrCodeConfigService;
        private readonly IDeviceConfigService _deviceConfigService;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsKeyboardInputVisible))]
        [NotifyPropertyChangedFor(nameof(IsSerialPortVisible))]
        [NotifyPropertyChangedFor(nameof(IsEthernetVisible))]
        [NotifyPropertyChangedFor(nameof(IsPlcRegisterVisible))]
        [NotifyPropertyChangedFor(nameof(IsBatchModeVisible))]
        private QrCodeConfig config;

        // 数据源可见性
        public bool IsKeyboardInputVisible => Config?.IsEnabled == true && Config?.SourceType == QrCodeSourceType.KeyboardInput;
        public bool IsSerialPortVisible => Config?.IsEnabled == true && Config?.SourceType == QrCodeSourceType.SerialPort;
        public bool IsEthernetVisible => Config?.IsEnabled == true && Config?.SourceType == QrCodeSourceType.Ethernet;
        public bool IsPlcRegisterVisible => Config?.IsEnabled == true && Config?.SourceType == QrCodeSourceType.PlcRegister;
        public bool IsBatchModeVisible => Config?.IsEnabled == false;

        // 以太网协议选项
        public Array ProtocolList => Enum.GetValues<NetworkProtocol>();
        // 数据源类型选项
        public ObservableCollection<QrCodeSourceType> SourceTypes { get; } = new()
        {
            QrCodeSourceType.KeyboardInput,
            QrCodeSourceType.SerialPort,
            QrCodeSourceType.Ethernet,
            QrCodeSourceType.PlcRegister
        };

        // PLC设备列表
        public ObservableCollection<PlcDevice> PlcDevices => _deviceConfigService.Devices;

        /// <summary>
        /// 可用串口列表
        /// </summary>
        public ObservableCollection<string> AvailableComPorts { get; } = new();

        public QrCodeSettingViewModel(ILog log, IQrCodeConfigService qrCodeConfigService, IDeviceConfigService deviceConfigService)
        {
            _log = log;
            _qrCodeConfigService = qrCodeConfigService;
            _deviceConfigService = deviceConfigService;

            // 监听配方和设备变化
            if (_qrCodeConfigService is System.ComponentModel.INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(IRecipeConfigService.CurrentRecipe))
                    {
                        _ = LoadConfigAsync();
                    }
                    else if (e.PropertyName == nameof(IDeviceConfigService.Devices))
                    {
                        OnPropertyChanged(nameof(PlcDevices));
                    }
                };
            }

            // 加载配置
            _ = LoadConfigAsync();
            RefreshComPorts();
        }

        /// <summary>
        /// 刷新可用串口列表
        /// </summary>
        [RelayCommand]
        private void RefreshComPorts()
        {
            AvailableComPorts.Clear();
            foreach (var port in SerialPort.GetPortNames())
            {
                AvailableComPorts.Add(port);
                if (AvailableComPorts.Count > 0)
                {
                    Config?.SerialPortName = AvailableComPorts[0];
                }
            }
        }

        /// <summary>
        /// 获取当前选中设备的点位列表
        /// </summary>
        public ObservableCollection<DataPoint> AvailablePoints
        {
            get
            {
                var points = new ObservableCollection<DataPoint>();
                if (Config?.SelectedPlcDevice != null)
                {
                    var devicePoints = _deviceConfigService.GetDataPointsByDeviceId(Config.SelectedPlcDevice.DeviceId);
                    foreach (var point in devicePoints)
                    {
                        points.Add(point);
                    }
                }
                return points;
            }
        }

        private Task LoadConfigAsync()
        {
            Config = _qrCodeConfigService.QrCodeConfig;
            _log.Info("二维码配置加载完成");
            return Task.CompletedTask;
        }

        private PlcDevice? _lastDevice;

        partial void OnConfigChanged(QrCodeConfig? oldValue, QrCodeConfig? newValue)
        {
            if (oldValue != null)
            {
                oldValue.PropertyChanged -= Config_PropertyChanged;
                if (_lastDevice != null)
                {
                    _lastDevice.DataPoints.CollectionChanged -= DataPoints_CollectionChanged;
                }
            }

            if (newValue != null)
            {
                // 恢复选中的设备和点位
                RestoreSelectedDeviceAndPoint();

                newValue.PropertyChanged += Config_PropertyChanged;

                _lastDevice = newValue.SelectedPlcDevice;
                if (_lastDevice != null)
                {
                    _lastDevice.DataPoints.CollectionChanged += DataPoints_CollectionChanged;
                }

                // 触发依赖属性刷新
                OnPropertyChanged(nameof(IsKeyboardInputVisible));
                OnPropertyChanged(nameof(IsSerialPortVisible));
                OnPropertyChanged(nameof(IsEthernetVisible));
                OnPropertyChanged(nameof(IsPlcRegisterVisible));
                OnPropertyChanged(nameof(IsBatchModeVisible));
                OnPropertyChanged(nameof(AvailablePoints));
            }
        }

        private void Config_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (Config == null) return;

            if (e.PropertyName == nameof(Config.IsEnabled))
            {
                // 当禁用二维码时，清空测试数据
                if (!Config.IsEnabled)
                {
                    Config.TestRawData = string.Empty;
                    Config.TestExtractedQrCode = string.Empty;
                    Config.TestValidationResult = string.Empty;
                }

                OnPropertyChanged(nameof(IsKeyboardInputVisible));
                OnPropertyChanged(nameof(IsSerialPortVisible));
                OnPropertyChanged(nameof(IsEthernetVisible));
                OnPropertyChanged(nameof(IsPlcRegisterVisible));
                OnPropertyChanged(nameof(IsBatchModeVisible));
            }
            else if (e.PropertyName == nameof(Config.SourceType))
            {
                OnPropertyChanged(nameof(IsKeyboardInputVisible));
                OnPropertyChanged(nameof(IsSerialPortVisible));
                OnPropertyChanged(nameof(IsEthernetVisible));
                OnPropertyChanged(nameof(IsPlcRegisterVisible));
                OnPropertyChanged(nameof(IsBatchModeVisible));
                if (Config.SourceType == QrCodeSourceType.PlcRegister)
                {
                    if (PlcDevices.Count > 0)
                    {
                        Config.SelectedPlcDevice = PlcDevices[0];
                        // 安全地选择第一个点位（如果存在）
                        if (Config.SelectedPlcDevice.DataPoints?.Count > 0)
                        {
                            Config.SelectedPoint = Config.SelectedPlcDevice.DataPoints[0];
                        }
                        else
                        {
                            Config.SelectedPoint = null;
                        }
                    }
                }
            }
            else if (e.PropertyName == nameof(Config.SelectedPlcDevice))
            {
                if (_lastDevice != null)
                {
                    _lastDevice.DataPoints.CollectionChanged -= DataPoints_CollectionChanged;
                }

                _lastDevice = Config.SelectedPlcDevice;
                if (_lastDevice != null)
                {
                    _lastDevice.DataPoints.CollectionChanged += DataPoints_CollectionChanged;
                }

                OnPropertyChanged(nameof(AvailablePoints));
            }
        }

        private void DataPoints_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 通知界面刷新点位下拉框
            OnPropertyChanged(nameof(AvailablePoints));
            if (Config.SelectedPoint != null && Config.SelectedPlcDevice != null)
            {
                var exists = Config.SelectedPlcDevice.DataPoints.Any(p => p.PointId == Config.SelectedPoint.PointId);
                if (!exists)
                {
                    Config.SelectedPoint = null;
                }
            }
        }

        [RelayCommand]
        private async Task SaveConfiguration()
        {
            try
            {
                // 验证配置
                if (!ValidateConfig(out string error))
                {
                    Growl.Warning(error);
                    return;
                }

                _qrCodeConfigService.QrCodeConfig = Config;
                var success = await _qrCodeConfigService.SaveQrCodeConfigAsync();
                if (success)
                {
                    Growl.Success("二维码配置已保存");
                    _log.Info("保存二维码配置成功");
                }
                else
                {
                    Growl.Error("保存配置失败");
                    _log.Error("保存二维码配置失败");
                }
            }
            catch (Exception ex)
            {
                Growl.Error($"保存失败: {ex.Message}");
                _log.Error($"保存二维码配置异常: {ex.Message}");
            }
        }

        [RelayCommand]
        private void TestExtraction()
        {
            try
            {
                if (string.IsNullOrEmpty(Config.TestRawData))
                {
                    Growl.Warning("请先输入测试数据");
                    return;
                }

                var rawData = Config.TestRawData;
                var validationResult = new System.Text.StringBuilder();

                // 1. 校验起始符
                if (Config.EnableStartSymbol && !string.IsNullOrEmpty(Config.StartSymbol))
                {
                    if (!rawData.StartsWith(Config.StartSymbol))
                    {
                        validationResult.AppendLine($"❌ 起始符校验失败：期望'{Config.StartSymbol}'");
                        Config.TestValidationResult = validationResult.ToString();
                        Config.TestExtractedQrCode = string.Empty;
                        return;
                    }
                    validationResult.AppendLine($"✅ 起始符校验通过：'{Config.StartSymbol}'");
                }

                // 2. 校验结束符
                if (Config.EnableEndSymbol && !string.IsNullOrEmpty(Config.EndSymbol))
                {
                    if (!rawData.EndsWith(Config.EndSymbol))
                    {
                        validationResult.AppendLine($"❌ 结束符校验失败：期望'{Config.EndSymbol}'");
                        Config.TestValidationResult = validationResult.ToString();
                        Config.TestExtractedQrCode = string.Empty;
                        return;
                    }
                    validationResult.AppendLine($"✅ 结束符校验通过：'{Config.EndSymbol}'");
                }

                // 3. 校验长度
                if (Config.EnableLengthCheck)
                {
                    if (rawData.Length != Config.ExpectedLength)
                    {
                        validationResult.AppendLine($"❌ 长度校验失败：期望{Config.ExpectedLength}，实际{rawData.Length}");
                        Config.TestValidationResult = validationResult.ToString();
                        Config.TestExtractedQrCode = string.Empty;
                        return;
                    }
                    validationResult.AppendLine($"✅ 长度校验通过：{rawData.Length}");
                }

                // 4. 提取二维码
                if (Config.QrCodeStartIndex < 0 || Config.QrCodeStartIndex >= rawData.Length)
                {
                    validationResult.AppendLine($"❌ 起始索引超出范围：{Config.QrCodeStartIndex}");
                    Config.TestValidationResult = validationResult.ToString();
                    Config.TestExtractedQrCode = string.Empty;
                    return;
                }

                var endIndex = Config.QrCodeStartIndex + Config.QrCodeLength;
                if (endIndex > rawData.Length)
                {
                    validationResult.AppendLine($"❌ 提取长度超出范围：起始{Config.QrCodeStartIndex}，长度{Config.QrCodeLength}");
                    Config.TestValidationResult = validationResult.ToString();
                    Config.TestExtractedQrCode = string.Empty;
                    return;
                }

                var extractedCode = rawData.Substring(Config.QrCodeStartIndex, Config.QrCodeLength);
                Config.TestExtractedQrCode = extractedCode;
                validationResult.AppendLine($"✅ 成功提取二维码：{extractedCode}");

                Config.TestValidationResult = validationResult.ToString();
                Growl.Success("测试提取成功");
                _log.Info($"测试提取成功：{extractedCode}");
            }
            catch (Exception ex)
            {
                Config.TestValidationResult = $"❌ 测试异常：{ex.Message}";
                Config.TestExtractedQrCode = string.Empty;
                Growl.Error($"测试失败: {ex.Message}");
                _log.Error($"测试提取异常: {ex.Message}");
            }
        }

        [RelayCommand]
        private void GenerateBatchNumber()
        {
            try
            {
                if (!Config.IsEnabled)
                {
                    var currentDate = DateTime.Now.ToString(Config.BatchDateFormat);

                    // 如果日期变了，重置流水号
                    if (Config.LastBatchDate != currentDate)
                    {
                        Config.LastBatchDate = currentDate;
                        Config.CurrentBatchSerial = 1;
                    }

                    var serialNumber = Config.CurrentBatchSerial.ToString($"D{Config.BatchSerialDigits}");
                    var batchNumber = $"{Config.BatchPrefix}{currentDate}{serialNumber}";

                    Config.TestExtractedQrCode = batchNumber;
                    Config.TestValidationResult = $"✅ 生成批次流水号：{batchNumber}";
                    Config.CurrentBatchSerial++;

                    Growl.Success($"生成批次流水号：{batchNumber}");
                    _log.Info($"生成批次流水号：{batchNumber}");
                }
                else
                {
                    Growl.Warning("当前启用了二维码模式，请切换到批次流水号模式");
                }
            }
            catch (Exception ex)
            {
                Growl.Error($"生成批次流水号失败: {ex.Message}");
                _log.Error($"生成批次流水号异常: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ClearTestData()
        {
            Config.TestRawData = string.Empty;
            Config.TestExtractedQrCode = string.Empty;
            Config.TestValidationResult = string.Empty;
            Growl.Info("已清除测试数据");
        }

        private bool ValidateConfig(out string error)
        {
            error = string.Empty;

            if (Config.IsEnabled)
            {
                // 二维码模式验证
                switch (Config.SourceType)
                {
                    case QrCodeSourceType.SerialPort:
                        if (string.IsNullOrEmpty(Config.SerialPortName))
                        {
                            error = "请选择串口";
                            return false;
                        }
                        break;

                    case QrCodeSourceType.Ethernet:
                        if (string.IsNullOrEmpty(Config.EthernetIp))
                        {
                            error = "请输入以太网IP地址";
                            return false;
                        }
                        if (Config.EthernetPort <= 0 || Config.EthernetPort > 65535)
                        {
                            error = "以太网端口范围：1-65535";
                            return false;
                        }
                        break;

                    case QrCodeSourceType.PlcRegister:
                        if (Config.PlcDeviceId <= 0)
                        {
                            error = "请选择PLC设备";
                            return false;
                        }
                        if (string.IsNullOrEmpty(Config.Address))
                        {
                            error = "请选择点位地址";
                            return false;
                        }
                        break;
                }

                // 提取参数验证
                if (Config.QrCodeStartIndex < 0)
                {
                    error = "起始索引不能为负数";
                    return false;
                }

                if (Config.QrCodeLength <= 0)
                {
                    error = "提取长度必须大于0";
                    return false;
                }
            }
            else
            {
                // 批次流水号模式验证
                if (string.IsNullOrEmpty(Config.BatchPrefix))
                {
                    error = "请输入流水号前缀";
                    return false;
                }

                if (string.IsNullOrEmpty(Config.BatchDateFormat))
                {
                    error = "请输入日期格式";
                    return false;
                }

                if (Config.BatchSerialDigits <= 0 || Config.BatchSerialDigits > 10)
                {
                    error = "流水号位数范围：1-10";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 根据保存的ID恢复选中的设备和点位对象
        /// </summary>
        private void RestoreSelectedDeviceAndPoint()
        {
            if (Config == null) return;

            // 恢复选中的设备
            if (Config.PlcDeviceId > 0)
            {
                Config.SelectedPlcDevice = PlcDevices.FirstOrDefault(d => d.DeviceId == Config.PlcDeviceId);
            }

            // 恢复选中的点位
            if (!string.IsNullOrEmpty(Config.Address) && Config.SelectedPlcDevice != null)
            {
                var points = _deviceConfigService.GetDataPointsByDeviceId(Config.SelectedPlcDevice.DeviceId);
                Config.SelectedPoint = points.FirstOrDefault(p => p.PointId == Config.Address);
            }
        }
    }
}
