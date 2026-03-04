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

        [ObservableProperty]
        private bool isAcquiring;

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

        public HomeViewModel(
            ILog log,
            IRecipeConfigService recipeConfigService,
            IDeviceConfigService deviceConfigService,
            IUserSettingsService userSettingsService,
            IDataRecordService dataRecordService)
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
                        if (_channels != null)
                            _channels.CollectionChanged -= Channels_CollectionChanged;

                        _channels = CurrentRecipe?.Channels;

                        // 绑定新的
                        if (_channels != null)
                        {
                            _channels.CollectionChanged += Channels_CollectionChanged;
                            foreach (var ch in _channels)
                                ch.PropertyChanged += Channel_PropertyChanged;
                        }

                        OnPropertyChanged(nameof(CurrentRecipe));
                        OnPropertyChanged(nameof(Channels)); // 刷新 UI

                        // 加载配方的产品图片
                        ProductImagePath = CurrentRecipe?.ProductImagePath ?? string.Empty;

                        _log.Info($"当前配方已更新: {CurrentRecipe?.RecipeName}");
                    }
                };
            }

            // 初始化时也要绑定
            _channels = CurrentRecipe?.Channels;
            if (_channels != null)
            {
                _channels.CollectionChanged += Channels_CollectionChanged;
                foreach (var ch in _channels)
                    ch.PropertyChanged += Channel_PropertyChanged;
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
        }

        private void Channel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(Channels));
        }


        [RelayCommand]
        private async Task StartAcquisitionAsync()
        {
            if (CurrentRecipe == null)
            {
                Growl.Warning("未选择配方");
                return;
            }

            if (IsAcquiring) return;

            IsAcquiring = true;
            MeasurementStatus = "正在采集...";
            OverallResult = MeasurementResult.NotMeasured;
            _cts = new CancellationTokenSource();
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    foreach (var channel in Channels)
                    {
                        if (_cts.Token.IsCancellationRequested) break;
                        if (!channel.IsEnabled) continue;
                        if (string.IsNullOrEmpty(channel.PlcDeviceName))
                        {

                            channel.ChannelDescription = "请先绑定设备";
                            continue;
                        }

                        if (string.IsNullOrEmpty(channel.DataPointName))
                        {
                            channel.ChannelDescription = "先请绑定设备点位";
                            continue;
                        }
                        var value = GetChannelCurrentValue(channel);
                        if (value != null)
                        {
                            channel.UpdateMeasuredValue(value.Value);
                        }
                    }

                    // 实时刷新总判定
                    if (Channels.Any(c => c.Result != MeasurementResult.NotMeasured))
                    {
                        OverallResult = Channels.All(c => c.Result == MeasurementResult.Pass)
                            ? MeasurementResult.Pass
                            : MeasurementResult.Fail;
                    }

                    await Task.Delay(PollIntervalMs, _cts.Token);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                IsAcquiring = false;
            }
        }

        [RelayCommand]
        private async Task StopAcquisitionAsync()
        {
            _cts?.Cancel();
            _log.Info("停止数据采集");
            MeasurementStatus = "采集已停止";

            // 停止后统计并保存记录
            if (CurrentRecipe != null && Channels.Any(c => c.Result != MeasurementResult.NotMeasured))
            {
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
                    ChannelData = Channels.Select(c => new ChannelMeasurementData
                    {
                        ChannelNumber = c.ChannelNumber,
                        ChannelName = c.ChannelName,
                        StandardValue = c.StandardValue,
                        UpperTolerance = c.UpperTolerance,
                        LowerTolerance = c.LowerTolerance,
                        MeasuredValue = c.MeasuredValue,
                        Result = c.Result
                    }).ToList(),
                    StepNumber = 1,
                    TotalSteps = CurrentRecipe.TotalSteps
                };
                await _dataRecordService.SaveRecordAsync(record);
                _log.Info($"测量记录已保存: {OverallResult}");
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

            PassCount = 0;
            FailCount = 0;
            TotalCount = 0;
            _log.Info("数据已清除");
        }

        [RelayCommand]
        private void ImportProductImage()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All Files (*.*)|*.*",
                Title = "选择产品图片"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                ProductImagePath = openFileDialog.FileName;
                // 保存到当前配方
                if (CurrentRecipe != null)
                {
                    CurrentRecipe.ProductImagePath = openFileDialog.FileName;
                    _log.Info($"已设置产品图片: {openFileDialog.FileName}");
                }
            }
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



            var device = _deviceConfigService.Devices
                .FirstOrDefault(d => d.DeviceId == channel.PlcDeviceId);
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