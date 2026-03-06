using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services;
using MeasurementSoftware.Services.Config;
using MeasurementSoftware.Services.Logs;
using MeasurementSoftware.Services.UserSetting;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MeasurementSoftware.ViewModels
{
    public partial class HomeViewModel : ObservableViewModel
    {
        private readonly ILog _log;
        private readonly IRecipeConfigService _recipeConfigService;
        private readonly IDeviceConfigService _deviceConfigService;
        private readonly IUserSettingsService _userSettingsService;
        private readonly IDataRecordService _dataRecordService;

        [ObservableProperty]
        private string? productImagePath;

        [ObservableProperty]
        private string title = "测量数据采集";

        // 直接引用全局配置中的当前配方
        public MeasurementRecipe? CurrentRecipe => _recipeConfigService.CurrentRecipe;

        public IEnumerable<MeasurementChannel> Channels => CurrentRecipe?.Channels?.Where(c => c.IsEnabled) ?? Enumerable.Empty<MeasurementChannel>();

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

        [ObservableProperty]
        private int passCount;

        [ObservableProperty]
        private int failCount;

        [ObservableProperty]
        private int totalCount;

        /// <summary>
        /// 实时采集数据（用于趋势图绘制）
        /// </summary>
        //[ObservableProperty]
        //private ObservableCollection<RealTimeDataEventArgs> realTimeData = [];

        /// <summary>
        /// 趋势图最大显示点数
        /// </summary>
        private const int MaxTrendPoints = 10;

        [ObservableProperty]
        private double[] trendXs = [];

        [ObservableProperty]
        private double[] trendYs = [];

        /// <summary>
        /// 轮询间隔(ms)
        /// </summary>
        private const int PollIntervalMs = 500;

        private CancellationTokenSource? _cts;
        private ObservableCollection<MeasurementChannel>? _channels;

        public HomeViewModel(ILog log, IRecipeConfigService recipeConfigService, IDeviceConfigService deviceConfigService, IUserSettingsService userSettingsService, IDataRecordService dataRecordService)
        {
            _log = log;
            _recipeConfigService = recipeConfigService;
            _deviceConfigService = deviceConfigService;
            _userSettingsService = userSettingsService;
            _dataRecordService = dataRecordService;

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

                        ProductImagePath = CurrentRecipe?.ProductImagePath ?? string.Empty;
                        CurrentStep = 1;

                        _log.Info($"当前配方已更新: {CurrentRecipe?.RecipeName}");
                    }
                };
            }

            // 初始化时也要绑定
            _channels = CurrentRecipe?.Channels;
            SubscribeRecipe();
        }

        private void SubscribeRecipe()
        {
            if (_channels != null)
            {
                _channels.CollectionChanged += Channels_CollectionChanged;
                foreach (var ch in _channels)
                    ch.PropertyChanged += Channel_PropertyChanged;
            }

            // 监听配方属性变化（图片路径、标注等）
            if (CurrentRecipe != null)
                CurrentRecipe.PropertyChanged += CurrentRecipe_PropertyChanged;
        }

        private void UnsubscribeRecipe()
        {
            if (_channels != null)
                _channels.CollectionChanged -= Channels_CollectionChanged;

            if (CurrentRecipe != null)
                CurrentRecipe.PropertyChanged -= CurrentRecipe_PropertyChanged;
        }

        private void CurrentRecipe_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MeasurementRecipe.ProductImagePath))
            {
                ProductImagePath = CurrentRecipe?.ProductImagePath ?? string.Empty;
            }
        }
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
        }

        private void Channel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(Channels));
            if (e.PropertyName == nameof(MeasurementChannel.Annotation) || e.PropertyName == nameof(MeasurementChannel.IsEnabled))
                OnPropertyChanged(nameof(Annotations));
        }


        [RelayCommand]
        private async Task StartAcquisitionAsync()
        {
            if (CurrentRecipe == null)
            {
                Growl.Warning("未选择配方");
                return;
            }

            _recipeConfigService.SetCollect(true);
            OverallResult = MeasurementResult.NotMeasured;
            _cts = new CancellationTokenSource();

            // 清空通道和标注
            bool isStepMode = CurrentRecipe.EnableStepMode && CurrentRecipe.TotalSteps > 1;
            if (isStepMode && CurrentStep > 1)
            {
                // 工步模式且不是第1步：只清当前工步
                ResetStepChannels(CurrentStep);
            }
            else
            {
                // 非工步模式 或 工步模式第1步：清空所有通道
                foreach (var ch in Channels)
                {
                    ch.MeasuredValue = 0;
                    ch.Result = MeasurementResult.NotMeasured;
                    ch.HistoricalData.Clear();
                }
                ResetAllAnnotations();
            }
            UpdateAnnotationActiveState();

            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    var activeChannels = GetActiveChannels();

                    MeasurementStatus = isStepMode ? $"工步 {CurrentStep}/{CurrentRecipe.TotalSteps} 采集中..." : "采集中...";

                    foreach (var channel in activeChannels)
                    {
                        if (_cts.Token.IsCancellationRequested) break;
                        var value = GetChannelCurrentValue(channel);
                        if (value != null)
                        {
                            channel.UpdateMeasuredValue(value.Value);
                            channel.CheckResult();
                        }
                    }

                    // 实时同步标注颜色
                    SyncAnnotationResults();

                    await Task.Delay(PollIntervalMs, _cts.Token);
                }
            }
            catch (OperationCanceledException) { }
        }

        [RelayCommand]
        private async Task StopAcquisitionAsync()
        {
            _cts?.Cancel();
            _log.Info("停止数据采集");
            MeasurementStatus = "采集已停止";
            _recipeConfigService.SetCollect(false);

            if (CurrentRecipe == null) return;

            // 根据工步模式选择参与判定的通道
            bool isStepMode = CurrentRecipe.EnableStepMode && CurrentRecipe.TotalSteps > 1;
            var relevantChannels = isStepMode
                ? Channels.Where(c => c.StepNumber == CurrentStep).ToList()
                : Channels.ToList();

            if (relevantChannels.Count == 0) return;

            // 计算每个通道的最终结果值并判定OK/NG
            FinalizeChannelResults(relevantChannels);

            // 将通道结果同步到标注点
            SyncAnnotationResults();

            // 根据相关通道计算总体结果
            OverallResult = relevantChannels.All(c => c.Result == MeasurementResult.Pass)
                ? MeasurementResult.Pass
                : MeasurementResult.Fail;

            TotalCount++;
            if (OverallResult == MeasurementResult.Pass)
                PassCount++;
            else
                FailCount++;

            var record = new MeasurementRecord
            {
                RecipeId = CurrentRecipe.RecipeId,
                RecipeName = CurrentRecipe.RecipeName,
                MeasurementTime = DateTime.Now,
                OverallResult = OverallResult,
                ChannelData = [.. Channels.Select(c => new ChannelMeasurementData
                {
                    ChannelNumber = c.ChannelNumber,
                    ChannelName = c.ChannelName,
                    StandardValue = c.StandardValue,
                    UpperTolerance = c.UpperTolerance,
                    LowerTolerance = c.LowerTolerance,
                    MeasuredValue = c.MeasuredValue,
                    Result = c.Result
                })],
                StepNumber = CurrentStep,
                TotalSteps = CurrentRecipe.TotalSteps
            };
            await _dataRecordService.SaveRecordAsync(record);
            _log.Info($"测量记录已保存: {OverallResult}");
        }

        /// <summary>
        /// 获取当前应采集的通道（根据工步模式过滤）
        /// </summary>
        private IEnumerable<MeasurementChannel> GetActiveChannels()
        {
            var enabled = Channels.Where(c => c.IsEnabled && !string.IsNullOrEmpty(c.PlcDeviceName) && !string.IsNullOrEmpty(c.DataPointName));

            if (CurrentRecipe?.EnableStepMode == true && CurrentRecipe.TotalSteps > 1)
            {
                // 分步模式：只采集当前工步的通道
                return enabled.Where(c => c.StepNumber == CurrentStep);
            }

            // 同时模式：采集所有通道
            return enabled;
        }



        /// <summary>
        /// 自动计算实际最大工步数（取通道中最大的 StepNumber 和配方 TotalSteps 的较大值）
        /// </summary>
        private int GetMaxStep()
        {
            if (CurrentRecipe == null) return 1;
            int maxStep = CurrentRecipe.TotalSteps;
            if (CurrentRecipe.Channels.Count > 0)
            {
                var channelMax = CurrentRecipe.Channels.Max(c => c.StepNumber);
                if (channelMax > maxStep)
                    maxStep = channelMax;
            }
            return maxStep;
        }

        /// <summary>
        /// 重置指定工步的通道及标注结果为未测量
        /// </summary>
        private void ResetStepChannels(int stepNumber)
        {
            foreach (var ch in Channels.Where(c => c.StepNumber == stepNumber))
            {
                ch.MeasuredValue = 0;
                ch.Result = MeasurementResult.NotMeasured;
                ch.HistoricalData.Clear();
                ch.Annotation?.Result = MeasurementResult.NotMeasured;
            }
        }

        /// <summary>
        /// 重置所有标注结果为未测量
        /// </summary>
        private void ResetAllAnnotations()
        {
            foreach (var channel in Channels)
            {
                if (channel.Annotation != null)
                    channel.Annotation.Result = MeasurementResult.NotMeasured;
            }
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
                channel.Annotation?.Result = channel.Result;
            }
        }

        /// <summary>
        /// 更新标注点的当前工步激活状态（控制标注可见性）
        /// </summary>
        private void UpdateAnnotationActiveState()
        {
            if (CurrentRecipe == null) return;
            bool isStepMode = CurrentRecipe.EnableStepMode && CurrentRecipe.TotalSteps > 1;
            foreach (var channel in Channels)
            {
                channel.Annotation?.IsActiveInCurrentStep = !isStepMode || channel.StepNumber == CurrentStep;
            }
        }

        /// <summary>
        /// 切换到下一个工步
        /// </summary>
        [RelayCommand]
        private void NextStep()
        {
            if (CurrentRecipe == null) return;

            int maxStep = GetMaxStep();

            if (CurrentStep < maxStep)
            {
                // 先结算当前工步的OK/NG并同步到标注
                var currentStepChannels = Channels.Where(c => c.StepNumber == CurrentStep).ToList();
                FinalizeChannelResults(currentStepChannels);
                SyncAnnotationResults();

                CurrentStep++;
                UpdateAnnotationActiveState();
                MeasurementStatus = $"已切换到工步 {CurrentStep}/{maxStep}";
            }
            else
            {
                Growl.Warning("已是最后一个工步");
            }
        }

        /// <summary>
        /// 切换到上一个工步
        /// </summary>
        [RelayCommand]
        private void PreviousStep()
        {
            if (CurrentRecipe == null) return;

            if (CurrentStep > 1)
            {
                CurrentStep--;
                UpdateAnnotationActiveState();
                MeasurementStatus = $"已切换到工步 {CurrentStep}/{GetMaxStep()}";
            }
            else
            {
                Growl.Warning("已是第一个工步");
            }
        }



        [RelayCommand]
        private async Task SaveRecipeAsync()
        {
            if (CurrentRecipe == null)
            {
                Growl.Warning("没有配方需要保存");
                return;
            }

            CurrentRecipe.ModifyTime = DateTime.Now;
            var success = await _recipeConfigService.SaveCurrentRecipeAsync();
            if (success)
                Growl.Success("配方保存成功");
            else
                Growl.Error("配方保存失败");
        }

        [RelayCommand]
        private void ClearData()
        {
            foreach (var channel in Channels)
            {
                channel.MeasuredValue = 0;
                channel.Result = MeasurementResult.NotMeasured;
                channel.HistoricalData.Clear();
            }

            // 重置标注颜色
            foreach (var channel in Channels)
            {
                channel.Annotation?.Result = MeasurementResult.NotMeasured;
            }

            OverallResult = MeasurementResult.NotMeasured;
            MeasurementStatus = "就绪";
            PassCount = 0;
            FailCount = 0;
            TotalCount = 0;
            CurrentStep = 1;
            _log.Info("数据已清除");
        }

        /// <summary>
        /// 直接从通道绑定的PLC数据点获取当前值（设备轮询已在更新 DataPoint.CurrentValue）
        /// </summary>
        private double? GetChannelCurrentValue(MeasurementChannel channel)
        {
            if (channel.PlcDeviceId == 0 || string.IsNullOrEmpty(channel.DataPointId))
            {
                channel.ChannelDescription = "没有找到对应的通道设备";
                return null;
            }

            var device = _deviceConfigService.Devices.FirstOrDefault(d => d.DeviceId == channel.PlcDeviceId);
            if (device == null)
            {
                channel.ChannelDescription = $"没有找到设备ID {channel.PlcDeviceId}";
                return null;
            }
            if (!device.IsEnabled)
            {
                channel.ChannelDescription = $"设备 {channel.PlcDeviceName} 未启用";
                return null;
            }
            if (!device.IsConnected)
            {
                channel.ChannelDescription = $"设备 {device.DeviceName} 未连接";
                return null;
            }
            var dataPoint = device.DataPoints.FirstOrDefault(dp => dp.PointId == channel.DataPointId);
            if (dataPoint?.CurrentValue == null || !dataPoint.IsSuccess)
            {
                channel.ChannelDescription = $"{dataPoint?.ErrorMessage}";
                return null;
            }
            else
            {
                channel.ChannelDescription = string.Empty;
            }

            try
            {
                return Convert.ToDouble(dataPoint.CurrentValue);
            }
            catch { return null; }
        }
    }
}