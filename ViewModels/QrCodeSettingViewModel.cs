using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Config;
using MeasurementSoftware.Services.Logs;
using MeasurementSoftware.Services.QrCodes;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO.Ports;
using System.Threading;

namespace MeasurementSoftware.ViewModels
{
    public partial class QrCodeSettingViewModel : ObservableViewModel
    {
        private readonly ILog _log;
        private readonly IQrCodeConfigService _qrCodeConfigService;
        private readonly IDeviceConfigService _deviceConfigService;
        private readonly EnabledPlcDevicesObserver _enabledDevicesObserver;
        private readonly IQrCodeScanService _qrCodeScanService;
        private CancellationTokenSource? _listenValidationCancellationTokenSource;

        /// <summary>
        /// 当前二维码配置。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsKeyboardInputVisible))]
        [NotifyPropertyChangedFor(nameof(IsSerialPortVisible))]
        [NotifyPropertyChangedFor(nameof(IsEthernetVisible))]
        [NotifyPropertyChangedFor(nameof(IsPlcRegisterVisible))]
        [NotifyPropertyChangedFor(nameof(IsBatchModeVisible))]
        private QrCodeConfig config;

        private bool _isListeningValidation;
        private string _listenValidationStatus = string.Empty;

        // 数据源可见性
        public bool IsKeyboardInputVisible => Config?.IsEnabled == true && Config?.SourceType == QrCodeSourceType.KeyboardInput;
        public bool IsSerialPortVisible => Config?.IsEnabled == true && Config?.SourceType == QrCodeSourceType.SerialPort;
        public bool IsEthernetVisible => Config?.IsEnabled == true && Config?.SourceType == QrCodeSourceType.Ethernet;
        public bool IsPlcRegisterVisible => Config?.IsEnabled == true && Config?.SourceType == QrCodeSourceType.PlcRegister;
        public bool IsBatchModeVisible => Config?.IsEnabled == false;
        private readonly IRecipeConfigService _recipeConfigService;

        /// <summary>
        /// 当前打开的配方。
        /// </summary>
        public MeasurementRecipe? CurrentRecipe => _recipeConfigService.CurrentRecipe;

        public bool IsListeningValidation
        {
            get => _isListeningValidation;
            set
            {
                if (SetProperty(ref _isListeningValidation, value))
                {
                    OnPropertyChanged(nameof(CanStartListenValidation));
                    OnPropertyChanged(nameof(CanStopListenValidation));
                }
            }
        }

        public string ListenValidationStatus
        {
            get => _listenValidationStatus;
            set => SetProperty(ref _listenValidationStatus, value);
        }

        public bool CanStartListenValidation => Config?.IsEnabled == true && !IsListeningValidation;

        public bool CanStopListenValidation => IsListeningValidation;

        /// <summary>
        /// 以太网协议选项。
        /// </summary>
        public Array ProtocolList => Enum.GetValues<NetworkProtocol>();

        /// <summary>
        /// 二维码数据源类型选项。
        /// </summary>
        public ObservableCollection<QrCodeSourceType> SourceTypes { get; } = new()
        {
            QrCodeSourceType.KeyboardInput,
            QrCodeSourceType.SerialPort,
            QrCodeSourceType.Ethernet,
            QrCodeSourceType.PlcRegister
        };

        /// <summary>
        /// 可用串口列表
        /// </summary>
        public ObservableCollection<string> AvailableComPorts { get; } = [];

        /// <summary>
        /// 可选 PLC 设备。
        /// 仅显示已启用设备，并随设备启用状态实时联动。
        /// </summary>
        public ReadOnlyObservableCollection<PlcDevice> PlcDevices => _enabledDevicesObserver.EnabledDevicesView;

        /// <summary>
        /// 创建二维码设置页面的视图模型。
        /// </summary>
        public QrCodeSettingViewModel(ILog log, IQrCodeConfigService qrCodeConfigService, IDeviceConfigService deviceConfigService, IRecipeConfigService recipeConfigService, IQrCodeScanService qrCodeScanService)
        {
            _log = log;
            _qrCodeConfigService = qrCodeConfigService;
            _deviceConfigService = deviceConfigService;
            _recipeConfigService = recipeConfigService;
            _qrCodeScanService = qrCodeScanService;
            _enabledDevicesObserver = new EnabledPlcDevicesObserver(_deviceConfigService);

            // 监听配方变化
            if (_recipeConfigService is INotifyPropertyChanged recipeNpc)
            {
                recipeNpc.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(IRecipeConfigService.CurrentRecipe))
                    {
                        Config = _qrCodeConfigService.QrCodeConfig;
                        _enabledDevicesObserver.Rebind();
                        RestoreSelectedDeviceAndPoint();
                        OnPropertyChanged(nameof(AvailablePoints));
                    }
                };
            }

            _enabledDevicesObserver.Changed += (_, _) =>
            {
                RestoreSelectedDeviceAndPoint();
                OnPropertyChanged(nameof(AvailablePoints));
            };

            Config = _qrCodeConfigService.QrCodeConfig;
            RefreshComPorts();
            _enabledDevicesObserver.Rebind();
            UpdateListenValidationStatus();

        }

        /// <summary>
        /// 刷新可用串口列表。
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
        /// 获取当前选中设备的启用点位列表。
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

        /// <summary>
        /// 上一次绑定点位集合监听的设备。
        /// 设备切换时用于移除旧监听。
        /// </summary>
        private PlcDevice? _lastDevice;

        partial void OnConfigChanged(QrCodeConfig? oldValue, QrCodeConfig newValue)
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
                RestoreSelectedDeviceAndPoint();

                newValue.PropertyChanged += Config_PropertyChanged;

                _lastDevice = newValue.SelectedPlcDevice;
                if (_lastDevice != null)
                {
                    _lastDevice.DataPoints.CollectionChanged += DataPoints_CollectionChanged;
                }

                OnPropertyChanged(nameof(IsKeyboardInputVisible));
                OnPropertyChanged(nameof(IsSerialPortVisible));
                OnPropertyChanged(nameof(IsEthernetVisible));
                OnPropertyChanged(nameof(IsPlcRegisterVisible));
                OnPropertyChanged(nameof(IsBatchModeVisible));
                OnPropertyChanged(nameof(AvailablePoints));
                UpdateListenValidationStatus();
            }
        }

        private void Config_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (Config == null)
            {
                return;
            }

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
                OnPropertyChanged(nameof(CanStartListenValidation));
            }
            else if (e.PropertyName == nameof(Config.SourceType))
            {
                OnPropertyChanged(nameof(IsKeyboardInputVisible));
                OnPropertyChanged(nameof(IsSerialPortVisible));
                OnPropertyChanged(nameof(IsEthernetVisible));
                OnPropertyChanged(nameof(IsPlcRegisterVisible));
                OnPropertyChanged(nameof(IsBatchModeVisible));
                OnPropertyChanged(nameof(CanStartListenValidation));

                if (Config.SourceType == QrCodeSourceType.PlcRegister && PlcDevices.Count > 0)
                {
                    Config.SelectedPlcDevice = PlcDevices[0];
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

                EnsureSelectedPoint();
                OnPropertyChanged(nameof(AvailablePoints));
            }

            if (!IsListeningValidation
                && e.PropertyName != nameof(Config.TestRawData)
                && e.PropertyName != nameof(Config.TestExtractedQrCode)
                && e.PropertyName != nameof(Config.TestValidationResult)
                && e.PropertyName != nameof(Config.RuntimeDisplayText)
                && e.PropertyName != nameof(Config.RuntimeValidationMessage)
                && e.PropertyName != nameof(Config.RuntimeValidationPassed))
            {
                UpdateListenValidationStatus();
            }
        }

        [RelayCommand]
        private async Task StartListenValidation()
        {
            try
            {
                if (IsListeningValidation)
                {
                    return;
                }

                if (!Config.IsEnabled)
                {
                    ListenValidationStatus = "当前为批次流水号模式，不能接收二维码";
                    Growl.Warning(ListenValidationStatus);
                    return;
                }

                if (!_qrCodeScanService.ValidateScanConfig(Config, out var error))
                {
                    Config.TestExtractedQrCode = string.Empty;
                    Config.TestValidationResult = error;
                    Config.RuntimeValidationPassed = false;
                    Config.RuntimeDisplayText = error;
                    Config.RuntimeValidationMessage = error;
                    ListenValidationStatus = $"当前配置不可接收二维码：{error}";
                    Growl.Warning(error);
                    return;
                }

                _listenValidationCancellationTokenSource?.Cancel();
                _listenValidationCancellationTokenSource?.Dispose();
                _listenValidationCancellationTokenSource = new CancellationTokenSource();

                IsListeningValidation = true;
                Config.TestExtractedQrCode = string.Empty;
                Config.TestValidationResult = "正在等待扫码数据...";
                ListenValidationStatus = $"监听已启动，{GetListeningStatusText()}。请手动点击“停止监听”结束。";

                while (!_listenValidationCancellationTokenSource.IsCancellationRequested)
                {
                    var result = await _qrCodeScanService.ReceiveAndValidateOnceAsync(Config, _listenValidationCancellationTokenSource.Token);
                    Config.TestRawData = result.RawData;
                    Config.TestExtractedQrCode = result.ExtractedQrCode;
                    Config.TestValidationResult = result.Message;
                    ListenValidationStatus = result.Success
                        ? "监听中：最近一次已接收到有效二维码，继续等待下一条数据。"
                        : "监听中：最近一次已接收到数据，但未通过当前配置校验，继续等待下一条数据。";
                }
            }
            catch (OperationCanceledException)
            {
                Config.TestValidationResult = "已停止监听验证";
                ListenValidationStatus = "已停止监听验证";
            }
            catch (Exception ex)
            {
                Config.TestExtractedQrCode = string.Empty;
                Config.TestValidationResult = $"❌ 监听异常：{ex.Message}";
                Config.RuntimeValidationPassed = false;
                Config.RuntimeDisplayText = ex.Message;
                Config.RuntimeValidationMessage = ex.Message;
                ListenValidationStatus = $"监听异常：{ex.Message}";
                Growl.Error($"监听验证失败: {ex.Message}");
                _log.Error($"监听验证异常: {ex.Message}");
            }
            finally
            {
                _listenValidationCancellationTokenSource?.Dispose();
                _listenValidationCancellationTokenSource = null;
                IsListeningValidation = false;
                if (Config.TestValidationResult != "已停止监听验证")
                {
                    ListenValidationStatus = "监听已结束";
                }
            }
        }

        [RelayCommand]
        private void StopListenValidation()
        {
            _listenValidationCancellationTokenSource?.Cancel();
        }

        private void DataPoints_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(AvailablePoints));
            if (Config?.SelectedPlcDevice == null)
            {
                return;
            }

            if (Config.SelectedPoint != null)
            {
                var exists = Config.SelectedPlcDevice.DataPoints.Any(p => p.PointId == Config.SelectedPoint.PointId);
                if (!exists)
                {
                    Config.SelectedPoint = null;
                }
            }

            EnsureSelectedPoint();
        }

        [RelayCommand]
        private async Task SaveConfiguration()
        {
            if (CurrentRecipe == null)
            {
                Growl.Warning("请先选择一个配方");
                return;
            }
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

                if (!_qrCodeScanService.TryExtractQrCode(Config, Config.TestRawData, out var extractedCode, out var validationResult))
                {
                    Config.TestValidationResult = validationResult;
                    Config.TestExtractedQrCode = string.Empty;
                    Growl.Warning("测试提取失败");
                    return;
                }

                Config.TestExtractedQrCode = extractedCode;
                Config.TestValidationResult = validationResult;
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
                    var batchNumber = _qrCodeScanService.GenerateBatchNumber(Config);
                    Config.TestExtractedQrCode = batchNumber;
                    Config.TestValidationResult = $"✅ 生成批次流水号：{batchNumber}";

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
                if (!_qrCodeScanService.ValidateScanConfig(Config, out error))
                {
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

        private void UpdateListenValidationStatus()
        {
            if (Config == null)
            {
                ListenValidationStatus = "当前未加载二维码配置";
                return;
            }

            if (!Config.IsEnabled)
            {
                ListenValidationStatus = "当前为批次流水号模式，不接收二维码";
                return;
            }

            if (!_qrCodeScanService.ValidateScanConfig(Config, out var error))
            {
                ListenValidationStatus = $"当前配置不可接收二维码：{error}";
                return;
            }

            ListenValidationStatus = $"当前配置可接收二维码，{GetListeningStatusText()}";
        }

        private string GetListeningStatusText()
        {
            return Config.SourceType switch
            {
                QrCodeSourceType.KeyboardInput => "等待键盘/扫码枪输入",
                QrCodeSourceType.SerialPort => $"正在等待串口 {Config.SerialPortName} 数据",
                QrCodeSourceType.Ethernet => $"正在等待 {Config.EthernetProtocol} {Config.EthernetIp}:{Config.EthernetPort} 数据",
                QrCodeSourceType.PlcRegister => $"正在等待 PLC 点位 {Config.Address} 的新值",
                _ => "正在等待二维码数据"
            };
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

            EnsureSelectedPoint();
        }

        /// <summary>
        /// 确保当前选中设备存在有效点位时，自动选中一个可用点位。
        /// </summary>
        private void EnsureSelectedPoint()
        {
            if (Config?.SelectedPlcDevice == null)
            {
                return;
            }

            var points = _deviceConfigService.GetDataPointsByDeviceId(Config.SelectedPlcDevice.DeviceId)
                .Where(p => p.IsEnabled)
                .ToList();

            if (points.Count == 0)
            {
                Config.SelectedPoint = null;
                return;
            }

            if (Config.SelectedPoint == null || points.All(p => p.PointId != Config.SelectedPoint.PointId))
            {
                Config.SelectedPoint = points[0];
            }
        }
    }
}
