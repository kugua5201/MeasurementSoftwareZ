using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using MeasurementSoftware.Extensions;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services;
using MeasurementSoftware.Services.Config;
using MeasurementSoftware.Services.Devices;
using MeasurementSoftware.Services.Logs;
using MeasurementSoftware.Services.QrCodes;
using MeasurementSoftware.Services.StepOperations;
using Microsoft.Win32;
using ScottPlot.ArrowShapes;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;

namespace MeasurementSoftware.ViewModels
{
    public partial class HomeViewModel : ObservableViewModel
    {
        private readonly ILog _log;
        private readonly IRecipeConfigService _recipeConfigService;
        private readonly IDataRecordService _dataRecordService;
        private readonly IPlcDeviceRuntimeService _plcDeviceRuntimeService;
        private readonly IQrCodeScanService _qrCodeScanService;
        private readonly IStepOperationMonitorService _stepOperationMonitorService;
        private DateTime? _acquisitionStartTime;
        private MeasurementRecipe? _subscribedRecipe;
        private QrCodeConfig? _subscribedQrCodeConfig;
        private bool _isWaitingForRequiredQrCode;
        private Task? _optionalQrCodeListeningTask;
        private string _scannedBarcode = string.Empty;
        private DateTime? _barcodeScanTime;

        [ObservableProperty]
        private string? productImagePath;

        [ObservableProperty]
        private string title = "测量数据采集";

        /// <summary>
        /// 当前配方
        /// </summary>
        public MeasurementRecipe? CurrentRecipe => _recipeConfigService.CurrentRecipe;

        public IEnumerable<MeasurementChannel> Channels => CurrentRecipe?.Channels?.Where(c => c.IsEnabled) ?? [];

        /// <summary>
        /// 图片标注点（用于主页展示，从通道中聚合）
        /// </summary>
        public IEnumerable<ChannelAnnotation> Annotations => Channels.Where(c => c.Annotation != null).Select(c => c.Annotation!);

        /// <summary>
        /// 当前工步编号
        /// </summary>
        [ObservableProperty]
        private int currentStep = 1;


        [ObservableProperty]
        private string measurementStatus = "就绪";

        [ObservableProperty]
        private MeasurementResult overallResult = MeasurementResult.NotMeasured;

        private string currentBarcode = "未扫码";

        public string CurrentBarcode
        {
            get => currentBarcode;
            set => SetProperty(ref currentBarcode, value);
        }

        private bool? currentBarcodeValidationPassed;

        public bool? CurrentBarcodeValidationPassed
        {
            get => currentBarcodeValidationPassed;
            set => SetProperty(ref currentBarcodeValidationPassed, value);
        }

        private string currentBarcodeValidationMessage = string.Empty;

        public string CurrentBarcodeValidationMessage
        {
            get => currentBarcodeValidationMessage;
            set => SetProperty(ref currentBarcodeValidationMessage, value);
        }

        /// <summary>
        /// 当前是否正在采集。
        /// 用于界面按钮状态和外部触发操作联动。
        /// </summary>
        public bool IsCollecting => _recipeConfigService.IsCollecting;

        public int PassCount
        {
            get => CurrentRecipe?.Statistics.PassCount ?? 0;
            set
            {
                if (CurrentRecipe?.Statistics == null || CurrentRecipe.Statistics.PassCount == value)
                {
                    return;
                }

                CurrentRecipe.Statistics.PassCount = value;
                OnPropertyChanged();
            }
        }

        public int FailCount
        {
            get => CurrentRecipe?.Statistics.FailCount ?? 0;
            set
            {
                if (CurrentRecipe?.Statistics == null || CurrentRecipe.Statistics.FailCount == value)
                {
                    return;
                }

                CurrentRecipe.Statistics.FailCount = value;
                OnPropertyChanged();
            }
        }

        public int TotalCount
        {
            get => CurrentRecipe?.Statistics.TotalCount ?? 0;
            set
            {
                if (CurrentRecipe?.Statistics == null || CurrentRecipe.Statistics.TotalCount == value)
                {
                    return;
                }

                CurrentRecipe.Statistics.TotalCount = value;
                OnPropertyChanged();
            }
        }


        /// <summary>
        /// 导出最大行数
        /// </summary>
        private const int MaxCsvRowsPerFile = 500000;

        /// <summary>
        /// 轮询间隔(ms)
        /// </summary>

        private int AcquisitionDelayMs => CurrentRecipe?.OtherSettings?.AcquisitionDelayMs ?? 500;

        private CancellationTokenSource? _cts;
        private ObservableCollection<MeasurementChannel>? _channels;

        public HomeViewModel(ILog log, IRecipeConfigService recipeConfigService, IDataRecordService dataRecordService, IPlcDeviceRuntimeService plcDeviceRuntimeService, IStepOperationMonitorService stepOperationMonitorService, IQrCodeScanService qrCodeScanService)
        {
            _log = log;
            _recipeConfigService = recipeConfigService;
            _dataRecordService = dataRecordService;
            _plcDeviceRuntimeService = plcDeviceRuntimeService;
            _stepOperationMonitorService = stepOperationMonitorService;
            _qrCodeScanService = qrCodeScanService;
            _stepOperationMonitorService.OperationTriggered += StepOperationMonitorService_OperationTriggered;

            // 不再从用户设置加载图片，图片跟随配方

            if (_recipeConfigService is INotifyPropertyChanged notifyPropertyChanged)
            {
                notifyPropertyChanged.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(IRecipeConfigService.CurrentRecipe))
                    {
                        // 解绑旧的
                        UnsubscribeRecipe();

                        _channels = CurrentRecipe?.Channels;

                        // 绑定新的
                        SubscribeRecipe();

                        OnPropertyChanged(nameof(CurrentRecipe));
                        OnPropertyChanged(nameof(Channels));
                        OnPropertyChanged(nameof(Annotations));
                        OnPropertyChanged(nameof(PassCount));
                        OnPropertyChanged(nameof(FailCount));
                        OnPropertyChanged(nameof(TotalCount));

                        ProductImagePath = CurrentRecipe?.BasicInfo.ProductImagePath ?? string.Empty;
                        CurrentStep = 1;
                        CurrentRecipe?.OtherSettings.HydrateStepOperationBindings(CurrentRecipe.Devices);
                        _stepOperationMonitorService.SetRecipe(CurrentRecipe);
                        ResetAllChannelStates();
                        RefreshCommandStates();

                        _log.Info($"当前配方已更新: {CurrentRecipe?.BasicInfo.RecipeName}");
                    }
                    else if (e.PropertyName == nameof(IRecipeConfigService.IsCollecting))
                    {
                        OnPropertyChanged(nameof(IsCollecting));
                        RefreshCommandStates();
                    }
                };
            }

            // 初始化时也要绑定
            _channels = CurrentRecipe?.Channels;
            SubscribeRecipe();
            CurrentRecipe?.OtherSettings.HydrateStepOperationBindings(CurrentRecipe.Devices);
            _stepOperationMonitorService.SetRecipe(CurrentRecipe);
            ResetAllChannelStates();
            RefreshCommandStates();
        }

        #region 配方订阅与界面联动

        /// <summary>
        /// 绑定当前配方相关事件。
        /// 这里集中处理集合变化、通道属性变化以及配方配置变化，便于调试时统一观察入口。
        /// </summary>
        private void SubscribeRecipe()
        {
            _subscribedRecipe = CurrentRecipe;

            if (_channels != null)
            {
                _channels.CollectionChanged += Channels_CollectionChanged;
                foreach (var ch in _channels)
                    ch.PropertyChanged += Channel_PropertyChanged;
            }

            // 监听配方基本信息变化（图片路径等）
            if (_subscribedRecipe?.BasicInfo != null)
                _subscribedRecipe.BasicInfo.PropertyChanged += BasicInfo_PropertyChanged;

            if (_subscribedRecipe?.OtherSettings != null)
                _subscribedRecipe.OtherSettings.PropertyChanged += OtherSettings_PropertyChanged;

            if (_subscribedRecipe?.Statistics != null)
                _subscribedRecipe.Statistics.PropertyChanged += Statistics_PropertyChanged;

            if (_subscribedRecipe?.QrCodeConfig != null)
            {
                _subscribedQrCodeConfig = _subscribedRecipe.QrCodeConfig;
                _subscribedQrCodeConfig.PropertyChanged += QrCodeConfig_PropertyChanged;
                SyncQrCodeRuntimeState();
            }
        }

        /// <summary>
        /// 解绑当前配方相关事件。
        /// 避免切换配方后仍然保留旧对象事件，导致状态串扰。
        /// </summary>
        private void UnsubscribeRecipe()
        {
            if (_channels != null)
                _channels.CollectionChanged -= Channels_CollectionChanged;

            if (_subscribedRecipe?.BasicInfo != null)
                _subscribedRecipe.BasicInfo.PropertyChanged -= BasicInfo_PropertyChanged;

            if (_subscribedRecipe?.OtherSettings != null)
                _subscribedRecipe.OtherSettings.PropertyChanged -= OtherSettings_PropertyChanged;

            if (_subscribedRecipe?.Statistics != null)
                _subscribedRecipe.Statistics.PropertyChanged -= Statistics_PropertyChanged;

            if (_subscribedQrCodeConfig != null)
            {
                _subscribedQrCodeConfig.PropertyChanged -= QrCodeConfig_PropertyChanged;
                _subscribedQrCodeConfig = null;
            }

            _subscribedRecipe = null;
        }

        /// <summary>
        /// 同步二维码运行时状态到测量页自己的显示状态。
        /// 测量页不直接依赖二维码设置页测试状态，但会消费扫码服务写入的运行时结果用于展示。
        /// </summary>
        private void QrCodeConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(QrCodeConfig.RuntimeDisplayText)
                or nameof(QrCodeConfig.RuntimeValidationMessage)
                or nameof(QrCodeConfig.RuntimeValidationPassed))
            {
                SyncQrCodeRuntimeState();
            }
        }

        private void SyncQrCodeRuntimeState()
        {
            if (_subscribedQrCodeConfig == null)
            {
                return;
            }

            void ApplyState()
            {
                CurrentBarcode = string.IsNullOrWhiteSpace(_subscribedQrCodeConfig.RuntimeDisplayText)
                    ? "未扫码"
                    : _subscribedQrCodeConfig.RuntimeDisplayText;

                CurrentBarcodeValidationPassed = _subscribedQrCodeConfig.RuntimeValidationPassed;
                CurrentBarcodeValidationMessage = _subscribedQrCodeConfig.RuntimeValidationMessage;

                if (_isWaitingForRequiredQrCode)
                {
                    MeasurementStatus = _subscribedQrCodeConfig.RuntimeValidationPassed == false
                        ? "扫码校验未通过，等待重新扫码..."
                        : "等待扫码...";
                }
            }

            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                _ = Application.Current.Dispatcher.InvokeAsync(ApplyState);
                return;
            }

            ApplyState();
        }

        /// <summary>
        /// 产品图片变化时，同步刷新主页显示。
        /// </summary>
        private void BasicInfo_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RecipeBasicInfoConfig.ProductImagePath))
            {
                ProductImagePath = CurrentRecipe?.BasicInfo.ProductImagePath ?? string.Empty;
            }
        }

        /// <summary>
        /// 配方其它设置变化时，刷新按钮状态。
        /// 当前主要关注分步模式与总工步数。
        /// </summary>
        private void OtherSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(RecipeOtherSettingsConfig.EnableStepMode) or nameof(RecipeOtherSettingsConfig.TotalSteps))
            {
                RefreshCommandStates();
            }
        }

        /// <summary>
        /// 统计信息变化时，主动通知界面刷新。
        /// </summary>
        private void Statistics_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RecipeStatisticsConfig.PassCount))
            {
                OnPropertyChanged(nameof(PassCount));
            }
            else if (e.PropertyName == nameof(RecipeStatisticsConfig.FailCount))
            {
                OnPropertyChanged(nameof(FailCount));
            }
            else if (e.PropertyName == nameof(RecipeStatisticsConfig.TotalCount))
            {
                OnPropertyChanged(nameof(TotalCount));
            }
        }

        /// <summary>
        /// 通道集合增删时，补充或移除属性监听，并刷新主界面绑定。
        /// </summary>
        private void Channels_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (MeasurementChannel c in e.NewItems)
                    c.PropertyChanged += Channel_PropertyChanged;
            if (e.OldItems != null)
                foreach (MeasurementChannel c in e.OldItems)
                    c.PropertyChanged -= Channel_PropertyChanged;
            OnPropertyChanged(nameof(Channels)); // 刷新 UI
            OnPropertyChanged(nameof(Annotations));
            RefreshCommandStates();
        }

        /// <summary>
        /// 通道属性变化时，刷新通道列表、标注以及顶部命令状态。
        /// </summary>
        private void Channel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(Channels));
            if (e.PropertyName == nameof(MeasurementChannel.Annotation) || e.PropertyName == nameof(MeasurementChannel.IsEnabled))
                OnPropertyChanged(nameof(Annotations));

            if (e.PropertyName is nameof(MeasurementChannel.IsEnabled) or nameof(MeasurementChannel.StepNumber))
            {
                RefreshCommandStates();
            }
        }

        partial void OnCurrentStepChanged(int value)
        {
            RefreshCommandStates();
        }

        #endregion

        #region 顶部命令状态与切换条件

        /// <summary>
        /// 刷新顶部操作按钮的可用状态。
        /// 手动点击与外部触发共用同一套判断逻辑。
        /// </summary>
        private void RefreshCommandStates()
        {
            StartAcquisitionCommand.NotifyCanExecuteChanged();
            CompleteMeasurementCommand.NotifyCanExecuteChanged();
            TerminateMeasurementCommand.NotifyCanExecuteChanged();
            PreviousStepCommand.NotifyCanExecuteChanged();
            NextStepCommand.NotifyCanExecuteChanged();
            ClearDataCommand.NotifyCanExecuteChanged();
            ExportChannelDataCsvCommand.NotifyCanExecuteChanged();
            ExportAllChannelDataCsvCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// 是否允许开始采集。
        /// </summary>
        private bool CanStartAcquisition()
        {
            return CurrentRecipe != null && !_recipeConfigService.IsCollecting;
        }

        /// <summary>
        /// 是否允许停止采集。
        /// </summary>
        private bool CanCompleteMeasurement()
        {
            return CurrentRecipe != null && _recipeConfigService.IsCollecting && !_isWaitingForRequiredQrCode;
        }

        /// <summary>
        /// 是否允许终止测量。
        /// </summary>
        private bool CanTerminateMeasurement()
        {
            return CurrentRecipe != null && _recipeConfigService.IsCollecting;
        }

        /// <summary>
        /// 是否允许切换到上一步。
        /// </summary>
        private bool CanPreviousStep()
        {
            if (_isWaitingForRequiredQrCode)
            {
                return false;
            }

            if (CurrentRecipe?.OtherSettings.EnableStepMode != true || CurrentStep <= 1)
            {
                return false;
            }

            return CanSwitchStep(CurrentStep - 1);
        }

        /// <summary>
        /// 是否允许切换到下一步。
        /// </summary>
        private bool CanNextStep()
        {
            if (_isWaitingForRequiredQrCode)
            {
                return false;
            }

            if (CurrentRecipe?.OtherSettings.EnableStepMode != true)
            {
                return false;
            }

            if (CurrentStep >= GetMaxStep())
            {
                return false;
            }

            return CanSwitchStep(CurrentStep + 1);
        }

        /// <summary>
        /// 是否允许清空数据。
        /// </summary>
        private bool CanClearData()
        {
            return CurrentRecipe != null && !_recipeConfigService.IsCollecting;
        }

        /// <summary>
        /// 判断目标工步当前是否允许切换。
        /// </summary>
        private bool CanSwitchStep(int targetStep)
        {
            if (CurrentRecipe == null)
            {
                return false;
            }

            if (!_recipeConfigService.IsCollecting)
            {
                return true;
            }

            return Channels.Any(c => c.IsEnabled && c.StepNumber == targetStep);
        }

        /// <summary>
        /// 判断当前配方是否工作在分步模式。
        /// 统一封装后，调试时更容易确认所有分支是否走对。
        /// </summary>
        private bool IsStepModeEnabled()
        {
            return CurrentRecipe?.OtherSettings.EnableStepMode == true && CurrentRecipe.OtherSettings.TotalSteps > 1;
        }

        /// <summary>
        /// 如果正在采集，切换工步前先校验目标工步是否有启用通道，避免切到空工步后被卡住。
        /// </summary>
        private bool CanSwitchStepDuringAcquisition(int targetStep)
        {
            if (!_recipeConfigService.IsCollecting)
            {
                return true;
            }

            var targetStepChannels = Channels.Where(c => c.IsEnabled && c.StepNumber == targetStep).ToList();

            if (targetStepChannels.Count == 0)
            {
                Growl.Warning($"工步 {targetStep} 没有启用通道，无法切换");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 自动计算实际最大工步数。
        /// 取通道中最大的 StepNumber 和配方 TotalSteps 的较大值，避免界面工步数与实际配置不一致。
        /// </summary>
        private int GetMaxStep()
        {
            if (CurrentRecipe == null)
            {
                return 1;
            }

            int maxStep = CurrentRecipe.OtherSettings.TotalSteps;
            if (CurrentRecipe.Channels.Count > 0)
            {
                var channelMax = CurrentRecipe.Channels.Max(c => c.StepNumber);
                if (channelMax > maxStep)
                {
                    maxStep = channelMax;
                }
            }

            return maxStep;
        }

        #endregion

        #region 工步触发事件处理

        /// <summary>
        /// 统一接收监听服务抛出的工步动作事件。
        /// 监听线程和触发条件判断已经在服务层处理，这里只负责按类型执行对应命令。
        /// </summary>
        private void StepOperationMonitorService_OperationTriggered(object? sender, StepOperationTriggeredEventArgs e)
        {
            if (Application.Current?.Dispatcher == null)
            {
                return;
            }

            _ = Application.Current.Dispatcher.InvokeAsync(() => ExecuteTriggeredStepOperationAsync(e.OperationType));
        }

        /// <summary>
        /// 根据事件类型执行开始、停止、上一步、下一步。
        /// 这里保留最小化判断，真正的监听条件由接口服务负责，页面只做命令分发。
        /// </summary>
        private Task ExecuteTriggeredStepOperationAsync(StepOperationType operationType)
        {
            return operationType switch
            {
                StepOperationType.StartAcquisition when CanStartAcquisition() => StartAcquisitionAsync(),
                StepOperationType.StopAcquisition when CanCompleteMeasurement() => CompleteMeasurementAsync(),
                StepOperationType.PreviousStep when CanPreviousStep() => ExecuteTriggeredStepSwitchAsync(PreviousStep),
                StepOperationType.NextStep when CanNextStep() => ExecuteTriggeredStepSwitchAsync(NextStep),
                StepOperationType.TerminateMeasurement when CanTerminateMeasurement() => TerminateMeasurementAsync(),
                _ => Task.CompletedTask
            };
        }

        /// <summary>
        /// 将同步的上下步切换统一包装为任务，便于事件入口复用。
        /// </summary>
        private static Task ExecuteTriggeredStepSwitchAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        #endregion

        #region 采集命令与轮询线程

        /// <summary>
        /// 开始采集。
        /// 该方法负责初始化本次采集上下文，并进入采集轮询线程。
        /// 调试时可重点看：是否误清空了错误范围的通道、当前工步是否正确、轮询是否正常退出。
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanStartAcquisition))]
        private async Task StartAcquisitionAsync()
        {
            if (CurrentRecipe == null)
            {
                Growl.Warning("请先选择一个配方");
                return;
            }

            if (_recipeConfigService.IsCollecting)
            {
                return;
            }

            _recipeConfigService.SetCollect(true);
            RefreshCommandStates();
            _acquisitionStartTime = DateTime.Now;
            CurrentStep = 1;
            OverallResult = MeasurementResult.NotMeasured;
            _cts = new CancellationTokenSource();

            // 每次重新启动测量，都按一次新的测量流程处理：
            // 清空全部表格状态，并把工步强制回到第 1 步，避免继续上次残留状态。
            ResetAllChannelStates();
            UpdateAnnotationActiveState();

            if (CurrentRecipe.QrCodeConfig.RequireQrCodeBeforeMeasurement)
            {
                try
                {
                    if (CurrentRecipe.QrCodeConfig.IsEnabled)
                    {
                        if (!_qrCodeScanService.ValidateScanConfig(CurrentRecipe.QrCodeConfig, out var scanConfigError))
                        {
                            CurrentBarcode = scanConfigError;
                            CurrentBarcodeValidationPassed = false;
                            _recipeConfigService.SetCollect(false);
                            RefreshCommandStates();
                            MeasurementStatus = "就绪";
                            Growl.Warning(scanConfigError);
                            return;
                        }

                        _isWaitingForRequiredQrCode = true;
                        RefreshCommandStates();
                        CurrentBarcode = "等待扫码";
                        CurrentBarcodeValidationPassed = null;
                CurrentBarcodeValidationMessage = string.Empty;
                        MeasurementStatus = "等待扫码...";
                        _scannedBarcode = await _qrCodeScanService.WaitForQrCodeAsync(CurrentRecipe.QrCodeConfig, _cts.Token);
                    }
                    else
                    {
                        _scannedBarcode = _qrCodeScanService.GenerateBatchNumber(CurrentRecipe.QrCodeConfig);
                        CurrentBarcodeValidationPassed = true;
                        MeasurementStatus = "已生成流水号，开始采集...";
                    }

                    _barcodeScanTime = DateTime.Now;
                    CurrentBarcode = _scannedBarcode;
                    CurrentBarcodeValidationPassed = true;
                    if (CurrentRecipe.QrCodeConfig.IsEnabled)
                    {
                        MeasurementStatus = "扫码成功，开始采集...";
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _isWaitingForRequiredQrCode = false;
                    _recipeConfigService.SetCollect(false);
                    RefreshCommandStates();
                    CurrentBarcode = ex.Message;
                    CurrentBarcodeValidationPassed = false;
                    MeasurementStatus = "就绪";
                    Growl.Error($"等待扫码失败: {ex.Message}");
                    _log.Error($"等待扫码失败: {ex.Message}");
                    return;
                }
                finally
                {
                    _isWaitingForRequiredQrCode = false;
                    RefreshCommandStates();
                }
            }
            else if (!CurrentRecipe.QrCodeConfig.IsEnabled)
            {
                _scannedBarcode = _qrCodeScanService.GenerateBatchNumber(CurrentRecipe.QrCodeConfig);
                _barcodeScanTime = DateTime.Now;
                CurrentBarcode = _scannedBarcode;
                CurrentBarcodeValidationPassed = true;
            }
            else
            {
                if (_qrCodeScanService.ValidateScanConfig(CurrentRecipe.QrCodeConfig, out var scanConfigError))
                {
                    CurrentBarcode = "等待扫码";
                    CurrentBarcodeValidationPassed = null;
                    CurrentBarcodeValidationMessage = string.Empty;
                    _optionalQrCodeListeningTask = ListenOptionalQrCodeDuringAcquisitionAsync(CurrentRecipe.QrCodeConfig, _cts.Token);
                }
                else
                {
                    CurrentBarcode = scanConfigError;
                    CurrentBarcodeValidationPassed = false;
                    _log.Warn($"二维码已启用，但配置无效，本次测量不读取二维码：{scanConfigError}");
                }
            }

            PrepareCurrentStepForAcquisition();

            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    var activeChannels = GetActiveChannels().ToList();
                    UpdateMeasurementStatusForCurrentContext();

                    foreach (var channel in activeChannels)
                    {
                        if (_cts.Token.IsCancellationRequested)
                        {
                            break;
                        }

                        var rawValue = GetChannelCurrentValue(channel);
                        if (rawValue == null)
                        {
                            continue;
                        }
                        bool useCacheHistory = channel.UseCacheValue && channel.RuntimeDevice?.SiemensReadCache.IsEnabled == true && !string.IsNullOrWhiteSpace(channel.RuntimeDataPoint?.CacheFieldKey);
                        if (useCacheHistory)
                        {
                            var cacheValues = _plcDeviceRuntimeService.TakeCacheFieldValues(channel.RuntimeDevice!, channel.RuntimeDataPoint!.CacheFieldKey);
                            // 启用硬件缓存时，历史数据按整批结构结果一次性写入；当前实时值只更新显示，不重复进历史。
                            if (cacheValues.Count > 0)
                            {
                                channel.AppendMeasuredValues(cacheValues, rawValue.Value);
                            }
                        }
                        else
                        {
                            // 常规轮询模式下，每轮只追加一个实时值到历史。
                            channel.UpdateMeasuredValue(rawValue.Value);
                        }

                        channel.DisplayState = MeasurementResult.Acquiring;
                    }

                    // 每轮采集后同步一次标注显示，便于调试 UI 颜色与结果是否一致。
                    SyncAnnotationResults();

                    await Task.Delay(AcquisitionDelayMs, _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }
        /// <summary>
        /// 获取通道当前值
        /// 统一使用通道当前绑定的运行时设备和点位对象取值。
        /// </summary>
        private double? GetChannelCurrentValue(MeasurementChannel channel)
        {
            var device = channel.RuntimeDevice;
            var dataPoint = channel.RuntimeDataPoint;

            if (device == null || dataPoint == null)
            {
                channel.ChannelDescription = "未绑定设备或点位";
                return null;
            }

            if (!device.IsEnabled)
            {
                channel.ChannelDescription = $"设备 {device.DeviceName} 未启用";
                return null;
            }
            if (!device.IsConnected)
            {
                channel.ChannelDescription = $"设备 {device.DeviceName} 未连接";
                return null;
            }

            if (dataPoint.CurrentValue == null || !dataPoint.IsSuccess)
            {
                channel.ChannelDescription = dataPoint.ErrorMessage ?? "读取中...";
                return null;
            }

            channel.ChannelDescription = string.Empty;
            try { return Convert.ToDouble(dataPoint.CurrentValue); }
            catch { return null; }
        }

        /// <summary>
        /// 停止采集。
        /// 该方法负责结束轮询线程、计算当前参与通道的结果、保存记录并更新统计信息。
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanCompleteMeasurement))]
        private async Task CompleteMeasurementAsync()
        {
            if (CurrentRecipe == null)
            {
                Growl.Warning("请先选择一个配方");
                return;
            }

            if (!_recipeConfigService.IsCollecting)
            {
                return;
            }

            _cts?.Cancel();
            _log.Info("停止数据采集");
            MeasurementStatus = "采集已停止";
            _isWaitingForRequiredQrCode = false;
            _optionalQrCodeListeningTask = null;
            _recipeConfigService.SetCollect(false);
            RefreshCommandStates();

            var relevantChannels = Channels.Where(c => c.IsMeasuredValueAvailable || c.IsResultValueAvailable).ToList();
            if (!relevantChannels.Any())
            {
                return;
            }

            // 停止时只对当前需要参与判定的通道做最终结果结算。
            FinalizeChannelResults(relevantChannels);
            ApplyResultDisplayStates(relevantChannels);
            SyncAnnotationResults();

            OverallResult = relevantChannels.All(c => c.Result == MeasurementResult.Pass) ? MeasurementResult.Pass : MeasurementResult.Fail;

            TotalCount++;
            if (OverallResult == MeasurementResult.Pass)
            {
                PassCount++;
            }
            else
            {
                FailCount++;
            }

            var record = new MeasurementRecord
            {
                RecipeId = CurrentRecipe.BasicInfo.RecipeId,
                RecipeName = CurrentRecipe.BasicInfo.RecipeName,
                MeasurementTime = DateTime.Now,
                IsStepMeasurement = IsStepModeEnabled(),
                OverallResult = OverallResult,
                Barcode = _scannedBarcode,
                BarcodeScanTime = _barcodeScanTime,
                ChannelData = [.. relevantChannels.Select(c => new ChannelMeasurementData
                {
                    ChannelNumber = c.ChannelNumber,
                    ChannelName = c.ChannelName,
                    ChannelDescription = c.ChannelDescription,
                    ChannelType = c.ChannelType.ToString(),
                    MeasurementType = c.MeasurementType,
                    DataSourceAddress = c.DataSourceAddress,
                    PlcDeviceName = c.PlcDeviceName,
                    DataPointName = c.DataPointName,
                    IsEnabled = c.IsEnabled,
                    DecimalPlaces = c.DecimalPlaces,
                    RequiresCalibration = c.RequiresCalibration,
                    CalibrationMode = c.CalibrationMode,
                    CalibrationCoefficientA = c.CalibrationCoefficientA,
                    CalibrationCoefficientB = c.CalibrationCoefficientB,
                    UseCacheValue = c.UseCacheValue,
                    SampleCount = c.SampleCount,
                    StandardValue = c.StandardValue,
                    UpperTolerance = c.UpperTolerance,
                    LowerTolerance = c.LowerTolerance,
                    MeasuredValue = c.MeasuredValue,
                    Unit = c.Unit,
                    StepNumber = c.StepNumber,
                    StepName = c.StepName,
                    Result = c.Result
                })],
                StepNumber = CurrentStep,
                TotalSteps = CurrentRecipe.OtherSettings.TotalSteps
            };

            await _dataRecordService.SaveRecordAsync(record);
            await _dataRecordService.SaveRecordToConfiguredFileAsync(record, CurrentRecipe);
            _log.Info($"测量记录已保存: {OverallResult}");
            _acquisitionStartTime = null;
        }

        /// <summary>
        /// 终止测量。
        /// 终止只负责立即结束当前采集，不做完成判定、不计数、也不保存记录。
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanTerminateMeasurement))]
        private Task TerminateMeasurementAsync()
        {
            if (CurrentRecipe == null)
            {
                Growl.Warning("请先选择一个配方");
                return Task.CompletedTask;
            }

            if (!_recipeConfigService.IsCollecting)
            {
                return Task.CompletedTask;
            }

            _cts?.Cancel();
            _isWaitingForRequiredQrCode = false;
            _optionalQrCodeListeningTask = null;
            _recipeConfigService.SetCollect(false);
            SetChannelDisplayState(GetActiveChannels(), MeasurementResult.Waiting);
            SyncAnnotationResults();
            MeasurementStatus = "测量已终止";
            _acquisitionStartTime = null;
            RefreshCommandStates();
            _log.Info("测量已终止");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 获取当前应参与采集轮询的通道。
        /// 分步模式下只采当前工步，同时模式下采所有已启用且已绑定的通道。
        /// </summary>
        private IEnumerable<MeasurementChannel> GetActiveChannels()
        {

            if (IsStepModeEnabled())
            {
                return Channels.Where(c => c.StepNumber == CurrentStep);
            }

            return Channels;
        }


        /// <summary>
        /// 根据当前模式刷新状态栏文本。
        /// </summary>
        private void UpdateMeasurementStatusForCurrentContext()
        {
            if (CurrentRecipe == null)
            {
                return;
            }

            MeasurementStatus = IsStepModeEnabled() ? $"工步 {CurrentStep}/{CurrentRecipe.OtherSettings.TotalSteps} 采集中..." : "采集中...";
        }


        #endregion

        #region 工步切换命令

        /// <summary>
        /// 切换到下一个工步。
        /// 如果当前处于采集中，会先完成当前工步的结果结算，再切到目标工步继续采集。
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanNextStep))]
        private void NextStep()
        {
            if (CurrentRecipe == null)
            {
                return;
            }

            var maxStep = GetMaxStep();
            if (CurrentStep >= maxStep)
            {
                Growl.Warning("已是最后一个工步");
                return;
            }

            SwitchStep(CurrentStep + 1, maxStep);
        }

        /// <summary>
        /// 切换到上一个工步。
        /// 如果当前处于采集中，会先完成当前工步的结果结算，再切到目标工步继续采集。
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanPreviousStep))]
        private void PreviousStep()
        {
            if (CurrentRecipe == null)
            {
                return;
            }

            if (CurrentStep <= 1)
            {
                Growl.Warning("已是第一个工步");
                return;
            }

            SwitchStep(CurrentStep - 1, GetMaxStep());
        }

        /// <summary>
        /// 执行实际工步切换。
        /// 这里统一收口上下步的共享逻辑，避免两边后续调试时出现修一边漏一边的情况。
        /// </summary>
        private void SwitchStep(int targetStep, int maxStep)
        {
            if (!CanSwitchStepDuringAcquisition(targetStep))
            {
                return;
            }

            if (_recipeConfigService.IsCollecting)
            {
                FinalizeCurrentStepBeforeSwitch();
            }

            CurrentStep = targetStep;

            if (_recipeConfigService.IsCollecting)
            {
                ResetStepChannels(CurrentStep);
                PrepareCurrentStepForAcquisition();
            }

            UpdateAnnotationActiveState();
            MeasurementStatus = _recipeConfigService.IsCollecting
                ? $"工步 {CurrentStep}/{maxStep} 采集中..."
                : $"已切换到工步 {CurrentStep}/{maxStep}";
        }

        /// <summary>
        /// 采集中切换工步前，先对当前工步做一次结果结算并刷新标注。
        /// </summary>
        private void FinalizeCurrentStepBeforeSwitch()
        {
            var currentStepChannels = Channels.Where(c => c.StepNumber == CurrentStep).ToList();
            FinalizeChannelResults(currentStepChannels);
            ApplyResultDisplayStates(currentStepChannels);
            SyncAnnotationResults();
        }

        #endregion

        #region 通道结果与标注状态整理

        private void ResetChannels(IEnumerable<MeasurementChannel> channels)
        {
            foreach (var channel in channels)
            {
                channel.ResetMeasurementState();

                if (channel.Annotation != null)
                {
                    channel.Annotation.Result = MeasurementResult.NotMeasured;
                    channel.Annotation.DisplayState = MeasurementResult.Waiting;
                }
            }
        }

        /// <summary>
        /// 重置当前页面所有通道状态。
        /// 作为整页初始化入口时，同时会清空总结果和状态文本。
        /// </summary>
        private void ResetAllChannelStates()
        {
            ResetChannels(Channels);
            _isWaitingForRequiredQrCode = false;
            _optionalQrCodeListeningTask = null;
            OverallResult = MeasurementResult.NotMeasured;
            _scannedBarcode = string.Empty;
            _barcodeScanTime = null;
            CurrentBarcode = "未扫码";
            CurrentBarcodeValidationPassed = null;
            CurrentBarcodeValidationMessage = string.Empty;
            MeasurementStatus = "就绪";
        }

        /// <summary>
        /// 非必须扫码模式下，测量启动后后台按二维码配置继续等待扫码。
        /// 读到后只更新当前编号，不阻塞通道采集；如果中途停止测量则跟随取消。
        /// </summary>
        private async Task ListenOptionalQrCodeDuringAcquisitionAsync(QrCodeConfig config, CancellationToken cancellationToken)
        {
            try
            {
                var barcode = await _qrCodeScanService.WaitForQrCodeAsync(config, cancellationToken);
                if (string.IsNullOrWhiteSpace(barcode))
                {
                    return;
                }

                _scannedBarcode = barcode;
                _barcodeScanTime = DateTime.Now;

                if (Application.Current?.Dispatcher != null)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        CurrentBarcode = barcode;
                        CurrentBarcodeValidationPassed = true;
                    });
                }
                else
                {
                    CurrentBarcode = barcode;
                    CurrentBarcodeValidationPassed = true;
                }

                _log.Info($"测量过程中已读取二维码：{barcode}");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _log.Warn($"测量过程中读取二维码失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 批量设置通道及其标注的显示状态。
        /// </summary>
        private void SetChannelDisplayState(IEnumerable<MeasurementChannel> channels, MeasurementResult displayState)
        {
            foreach (var channel in channels)
            {
                channel.DisplayState = displayState;
                if (channel.Annotation != null)
                {
                    channel.Annotation.DisplayState = displayState;
                }
            }
        }

        /// <summary>
        /// 开始采集当前工步前，先把本轮参与采集的通道显示状态改为“采集中”。
        /// </summary>
        private void PrepareCurrentStepForAcquisition()
        {
            SetChannelDisplayState(GetActiveChannels(), MeasurementResult.Acquiring);
            SyncAnnotationResults();
        }

        /// <summary>
        /// 重置指定工步的通道及标注结果为未测量
        /// </summary>
        private void ResetStepChannels(int stepNumber)
        {
            ResetChannels(Channels.Where(c => c.StepNumber == stepNumber));
        }


        /// <summary>
        /// 对通道集合计算最终结果值并判定OK/NG（调用 UpdateResultValue → CheckResult）
        /// </summary>
        private void FinalizeChannelResults(IEnumerable<MeasurementChannel> channels)
        {
            foreach (var ch in channels)
                ch.UpdateResultValue();
        }

        /// <summary>
        /// 将通道测量结果同步到对应的标注点
        /// </summary>
        private void SyncAnnotationResults()
        {
            foreach (var channel in Channels)
            {
                if (channel.Annotation == null)
                {
                    continue;
                }

                channel.Annotation.Result = channel.Result;
                channel.Annotation.DisplayState = channel.DisplayState;
            }
        }

        /// <summary>
        /// 根据最终结果刷新通道及标注显示状态。
        /// </summary>
        private void ApplyResultDisplayStates(IEnumerable<MeasurementChannel> channels)
        {
            foreach (var channel in channels)
            {
                channel.SetDisplayStateFromResult();

                if (channel.Annotation != null)
                {
                    channel.Annotation.Result = channel.Result;
                    channel.Annotation.DisplayState = channel.DisplayState;
                }
            }
        }

        /// <summary>
        /// 更新标注点的当前工步激活状态（控制标注可见性）。
        /// </summary>
        private void UpdateAnnotationActiveState()
        {
            if (CurrentRecipe == null)
            {
                return;
            }

            bool isStepMode = IsStepModeEnabled();
            foreach (var channel in Channels)
            {
                channel.Annotation?.IsActiveInCurrentStep = !isStepMode || channel.StepNumber == CurrentStep;
            }
        }

        #endregion

        #region 页面操作命令

        /// <summary>
        /// 保存当前配方。
        /// </summary>
        [RelayCommand]
        private async Task SaveRecipeAsync()
        {
            if (CurrentRecipe == null)
            {
                Growl.Warning("请先选择一个配方");
                return;
            }

            CurrentRecipe.BasicInfo.ModifyTime = DateTime.Now;
            var success = await _recipeConfigService.SaveCurrentRecipeAsync();
            if (success)
                Growl.Success("配方保存成功");
            else
                Growl.Warning("配方保存失败");
        }

        /// <summary>
        /// 清空当前页面采集数据，并重置统计与工步状态。
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanClearData))]
        private void ClearData()
        {
            ResetAllChannelStates();
            _recipeConfigService.ResetRecipeStatistics(CurrentRecipe);
            CurrentStep = 1;
            UpdateAnnotationActiveState();
            RefreshCommandStates();
            _log.Info("数据已清除");
        }

        /// <summary>
        /// 是否允许导出当前通道表格数据。
        /// 仅在未采集中允许，避免导出半途中间态。
        /// </summary>
        private bool CanExportChannelDataCsv()
        {
            return CurrentRecipe != null && !_recipeConfigService.IsCollecting && Channels.Any();
        }

        /// <summary>
        /// 将当前首页通道表格数据导出为 CSV。
        /// 支持导出通道当前缓存/寄存器采集后的显示结果，便于现场右键留档。
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanExportChannelDataCsv))]
        private async Task ExportChannelDataCsvAsync(MeasurementChannel channel)
        {
            if (!CanExportChannelDataCsv())
            {
                Growl.Warning("当前没有可导出的通道数据");
                return;
            }
            if (channel.HistoricalData.Count == 0)
            {
                Growl.Warning("当前通道导出数据为空");
                return;
            }

            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "请选择导出文件夹"
            };

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
            {
                return;
            }

            string exportDirectory = Path.Combine(dialog.SelectedPath, $"{channel.ChannelName}数据导出文件夹{DateTime.Now:yyyyMMddHHmmss}");

            Directory.CreateDirectory(exportDirectory);

            int fileIndex = 1;
            for (int start = 0; start < channel.HistoricalData.Count; start += MaxCsvRowsPerFile)
            {
                var builder = new StringBuilder();
                builder.AppendLine("通道名称,值");

                foreach (var value in channel.HistoricalData.Skip(start).Take(MaxCsvRowsPerFile))
                {
                    builder.AppendLine($"{channel.ChannelName},{value}");
                }

                string filePath = channel.HistoricalData.Count <= MaxCsvRowsPerFile
                    ? Path.Combine(exportDirectory, $"{channel.ChannelName}数据.csv")
                    : Path.Combine(exportDirectory, $"{channel.ChannelName}数据_{fileIndex}.csv");
                await File.WriteAllTextAsync(filePath, builder.ToString(), Encoding.UTF8);
                fileIndex++;
            }

            Growl.Success("通道数据已导出到 CSV");
            _log.Info($"通道数据导出成功: {exportDirectory}");
        }

        [RelayCommand(CanExecute = nameof(CanExportChannelDataCsv))]
        private async Task ExportAllChannelDataCsvAsync()
        {
            if (!CanExportChannelDataCsv())
            {
                Growl.Warning("当前没有可导出的通道数据");
                return;
            }
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "请选择导出文件夹"
            };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
            {
                return;
            }

            int maxRows = Channels.Max(c => c.HistoricalData.Count);
            string exportDirectory = Path.Combine(dialog.SelectedPath, $"全部通道数据导出文件夹{DateTime.Now:yyyyMMddHHmmss}");

            Directory.CreateDirectory(exportDirectory);

            int fileIndex = 1;
            for (int start = 0; start < maxRows; start += MaxCsvRowsPerFile)
            {
                var builder = new StringBuilder();
                builder.AppendLine(string.Join(",", Channels.SelectMany(c => new[] { "通道名称", "值" })));

                for (int i = start; i < Math.Min(start + MaxCsvRowsPerFile, maxRows); i++)
                {
                    builder.AppendLine(string.Join(",", Channels.SelectMany(c =>
                        i < c.HistoricalData.Count ? [c.ChannelName, c.HistoricalData[i].ToString()] : new[] { c.ChannelName, "" })));
                }

                string filePath = Path.Combine(exportDirectory, $"全部通道数据_{fileIndex}.csv");
                await File.WriteAllTextAsync(filePath, builder.ToString(), Encoding.UTF8);
                fileIndex++;
            }

            Growl.Success("通道数据已导出到 CSV");
            _log.Info($"全部通道数据导出成功: {exportDirectory}");
        }


        #endregion
    }
}