using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services;
using MeasurementSoftware.Services.Config;
using MeasurementSoftware.Services.Logs;
using System.Collections.ObjectModel;

namespace MeasurementSoftware.ViewModels
{
    public partial class CalibrationViewModel : ObservableViewModel
    {
        private readonly ILog _log;
        private readonly IRecipeConfigService _recipeConfigService;
        private readonly ICalibrationService _calibrationService;

        [ObservableProperty]
        private string currentRecipeName = "未加载配方";

        public ObservableCollection<MeasurementChannel> Channels => new(_recipeConfigService.CurrentRecipe?.Channels?.Where(c => c.IsEnabled) ?? []);


        [ObservableProperty]
        private MeasurementChannel? selectedChannel;

        /// <summary>
        /// 校准方式列表（供 ComboBox 绑定）
        /// </summary>
        public List<CalibrationMode> CalibrationModes { get; } =
            [CalibrationMode.SinglePoint, CalibrationMode.LeastSquares, CalibrationMode.LinearRegression];

        /// <summary>
        /// 当前选中的校准方式
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsSinglePointCalibration))]
        [NotifyPropertyChangedFor(nameof(IsLeastSquaresCalibration))]
        [NotifyPropertyChangedFor(nameof(IsLinearRegressionCalibration))]
        private CalibrationMode selectedCalibrationMode = CalibrationMode.SinglePoint;

        /// <summary>
        /// 是否单点校准（兼容旧绑定 + TabControl 可见性）
        /// </summary>
        public bool IsSinglePointCalibration => SelectedCalibrationMode == CalibrationMode.SinglePoint;

        public bool IsLeastSquaresCalibration => SelectedCalibrationMode == CalibrationMode.LeastSquares;

        public bool IsLinearRegressionCalibration => SelectedCalibrationMode == CalibrationMode.LinearRegression;

        [ObservableProperty]
        private double singlePointStandardValue;

        [ObservableProperty]
        private double singlePointMeasuredValue;

        [ObservableProperty]
        private double leastSquaresStandardValue;

        [ObservableProperty]
        private double leastSquaresMeasuredValue;

        [ObservableProperty]
        private double linearRegressionStandardValue;

        [ObservableProperty]
        private double linearRegressionMeasuredValue;

        [ObservableProperty]
        private ObservableCollection<LeastSquaresCalibrationPoint> leastSquaresCalibrationPoints = [];

        [ObservableProperty]
        private ObservableCollection<LinearRegressionCalibrationPoint> linearRegressionCalibrationPoints = [];

        [ObservableProperty]
        private LeastSquaresCalibrationPoint? selectedLeastSquaresCalibrationPoint;

        [ObservableProperty]
        private LinearRegressionCalibrationPoint? selectedLinearRegressionCalibrationPoint;

        [ObservableProperty]
        private ObservableCollection<CalibrationRecord> calibrationHistory = [];

        [ObservableProperty]
        private CalibrationRecord? selectedCalibrationHistory;



        [ObservableProperty]
        private string calibrationStatus = "--";

        [ObservableProperty]
        private string calibrationStatusColor = "#666";

        private ObservableCollection<MeasurementChannel>? _channels;

        public MeasurementRecipe? CurrentRecipe => _recipeConfigService.CurrentRecipe;

        public CalibrationViewModel(ILog log, IRecipeConfigService recipeConfigService, ICalibrationService calibrationService)
        {
            _log = log;
            _recipeConfigService = recipeConfigService;
            _calibrationService = calibrationService;

            if (_recipeConfigService is System.ComponentModel.INotifyPropertyChanged notifyPropertyChanged)
            {
                notifyPropertyChanged.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(IRecipeConfigService.CurrentRecipe))
                    {
                        BindChannels();
                        CurrentRecipeName = _recipeConfigService.CurrentRecipe?.BasicInfo.RecipeName ?? "未加载配方";
                        SelectedChannel = Channels.Any() ? Channels[0] : null;
                        OnPropertyChanged(nameof(Channels));
                    }
                };
            }

            BindChannels();
            CurrentRecipeName = _recipeConfigService.CurrentRecipe?.BasicInfo.RecipeName ?? "未加载配方";

        }

        private void BindChannels()
        {
            if (_channels != null)
            {
                _channels.CollectionChanged -= Channels_CollectionChanged;
                foreach (var ch in _channels)
                    ch.PropertyChanged -= Channel_PropertyChanged;
            }

            _channels = _recipeConfigService.CurrentRecipe?.Channels;

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

            OnPropertyChanged(nameof(Channels));
        }

        private void Channel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MeasurementChannel.IsEnabled))
            {
                OnPropertyChanged(nameof(Channels));
            }
        }

        partial void OnSelectedChannelChanged(MeasurementChannel? value)
        {
            if (value != null)
            {
                LoadCalibrationData(value);
                LoadCalibrationHistory();
            }
        }

        partial void OnSelectedCalibrationModeChanged(CalibrationMode value)
        {
            if (SelectedChannel != null)
            {
                SelectedChannel.CalibrationMode = value;
            }
        }

        partial void OnSinglePointStandardValueChanged(double value)
        {
            if (SelectedChannel != null)
            {
                SelectedChannel.SinglePointCalibration.StandardValue = value;
            }
        }

        partial void OnSinglePointMeasuredValueChanged(double value)
        {
            if (SelectedChannel != null)
            {
                SelectedChannel.SinglePointCalibration.MeasuredValue = value;
            }
        }

        partial void OnLeastSquaresStandardValueChanged(double value)
        {
            if (SelectedChannel != null)
            {
                SelectedChannel.LeastSquaresCalibration.InputStandardValue = value;
            }
        }

        partial void OnLeastSquaresMeasuredValueChanged(double value)
        {
            if (SelectedChannel != null)
            {
                SelectedChannel.LeastSquaresCalibration.InputMeasuredValue = value;
            }
        }

        partial void OnLinearRegressionStandardValueChanged(double value)
        {
            if (SelectedChannel != null)
            {
                SelectedChannel.LinearRegressionCalibration.InputStandardValue = value;
            }
        }

        partial void OnLinearRegressionMeasuredValueChanged(double value)
        {
            if (SelectedChannel != null)
            {
                SelectedChannel.LinearRegressionCalibration.InputMeasuredValue = value;
            }
        }

        private void LoadCalibrationData(MeasurementChannel channel)
        {
            SelectedCalibrationMode = channel.CalibrationMode;
            SinglePointStandardValue = channel.SinglePointCalibration.StandardValue;
            SinglePointMeasuredValue = channel.SinglePointCalibration.MeasuredValue;
            LeastSquaresStandardValue = channel.LeastSquaresCalibration.InputStandardValue;
            LeastSquaresMeasuredValue = channel.LeastSquaresCalibration.InputMeasuredValue;
            LinearRegressionStandardValue = channel.LinearRegressionCalibration.InputStandardValue;
            LinearRegressionMeasuredValue = channel.LinearRegressionCalibration.InputMeasuredValue;
            LeastSquaresCalibrationPoints = channel.LeastSquaresCalibration.Points;
            LinearRegressionCalibrationPoints = channel.LinearRegressionCalibration.Points;
        }



        private async void LoadCalibrationHistory()
        {
            if (SelectedChannel == null) return;

            var history = await _calibrationService.GetCalibrationHistoryAsync(SelectedChannel);
            CalibrationHistory = new ObservableCollection<CalibrationRecord>(history);
            SelectedCalibrationHistory = CalibrationHistory.FirstOrDefault();
        }

        [RelayCommand]
        private void ApplySelectedCalibrationHistory()
        {
            if (SelectedChannel == null)
            {
                Growl.Warning("请先选择通道");
                return;
            }

            if (SelectedCalibrationHistory == null)
            {
                Growl.Warning("请先选择一条校准历史");
                return;
            }

            var record = SelectedCalibrationHistory;

            SelectedChannel.CalibrationCoefficientA = record.CoefficientA;
            SelectedChannel.CalibrationCoefficientB = record.CoefficientB;

            Growl.Success($"已回用历史系数 A={record.CoefficientA:F6}, B={record.CoefficientB:F6}");
            _log.Info($"通道 {SelectedChannel.ChannelName} 已回用历史系数 A={record.CoefficientA:F6}, B={record.CoefficientB:F6}");
        }

        /// <summary>
        /// 从通道绑定的PLC数据点读取当前测量值
        /// </summary>
        [RelayCommand]
        private void ReadCurrentMeasuredValue()
        {
            if (SelectedChannel == null)
            {
                Growl.Error("请先选择通道");
                return;
            }

            var value = GetChannelCurrentValue(SelectedChannel);
            if (value == null)
            {
                Growl.Warning("无法读取当前值，请检查设备连接和点位配置");
                return;
            }

            if (IsSinglePointCalibration)
            {
                SinglePointMeasuredValue = value.Value;
            }
            else if (IsLeastSquaresCalibration)
            {
                LeastSquaresMeasuredValue = value.Value;
            }
            else
            {
                LinearRegressionMeasuredValue = value.Value;
            }

            _log.Info($"读取通道 {SelectedChannel.ChannelName} 当前值: {value.Value}");
        }

        /// <summary>
        /// 直接从通道绑定的PLC数据点获取当前值
        /// </summary>
        private double? GetChannelCurrentValue(MeasurementChannel channel)
        {
            var dataPoint = channel.RuntimeDataPoint;
            if (dataPoint == null)
                return null;
            if (dataPoint?.CurrentValue == null || !dataPoint.IsSuccess)
                return null;

            try { return Convert.ToDouble(dataPoint.CurrentValue); }
            catch { return null; }
        }

        [RelayCommand]
        private async Task ExecuteSinglePointCalibration()
        {
            if (SelectedChannel == null)
            {
                Growl.Warning("请先选择通道");
                return;
            }

            var result = await _calibrationService.CalibrateSinglePointAsync(
                SelectedChannel,
                SinglePointStandardValue,
                SinglePointMeasuredValue);

            if (result.Success)
            {
                Growl.Info($"单点校准成功！\n偏移量 B = {result.CoefficientB:F6}");
                LoadCalibrationHistory();
                _log.Info($"通道 {SelectedChannel.ChannelName} 单点校准成功");
            }
            else
            {
                Growl.Error("单点校准失败，请检查输入数据");
                _log.Error("单点校准失败");
            }
        }

        [RelayCommand]
        private void AddLeastSquaresCalibrationPoint()
        {
            var point = new LeastSquaresCalibrationPoint
            {
                Index = LeastSquaresCalibrationPoints.Count + 1,
                StandardValue = LeastSquaresStandardValue,
                MeasuredValue = LeastSquaresMeasuredValue
            };
            LeastSquaresCalibrationPoints.Add(point);
            _log.Info($"添加最小二乘法校准点: 标准值={point.StandardValue}, 测量值={point.MeasuredValue}");
        }

        [RelayCommand]
        private void RemoveLeastSquaresCalibrationPoint(LeastSquaresCalibrationPoint? point)
        {
            if (point == null)
            {
                Growl.Warning("请先选择要删除的最小二乘法校准点");
                return;
            }

            LeastSquaresCalibrationPoints.Remove(point);
            for (int i = 0; i < LeastSquaresCalibrationPoints.Count; i++)
            {
                LeastSquaresCalibrationPoints[i].Index = i + 1;
            }

            SelectedLeastSquaresCalibrationPoint = LeastSquaresCalibrationPoints.FirstOrDefault();
        }

        [RelayCommand]
        private void ClearLeastSquaresCalibrationPoints()
        {
            LeastSquaresCalibrationPoints.Clear();
        }

        [RelayCommand]
        private async Task ExecuteLeastSquaresCalibration()
        {
            if (SelectedChannel == null)
            {
                Growl.Warning("请先选择通道");
                return;
            }

            if (LeastSquaresCalibrationPoints.Count < 2)
            {
                Growl.Warning("最小二乘法校准至少需要2个校准点");
                return;
            }

            var points = LeastSquaresCalibrationPoints.Select(p => (p.StandardValue, p.MeasuredValue)).ToList();
            var result = await _calibrationService.CalibrateLeastSquaresAsync(SelectedChannel, points);

            if (result.Success)
            {
                Growl.Info($"最小二乘法校准成功！\n线性系数 A = {result.CoefficientA:F6}\n线性系数 B = {result.CoefficientB:F6}\n校准公式: y = {result.CoefficientA:F6} * x + {result.CoefficientB:F6}");
                LoadCalibrationHistory();
                _log.Info($"通道 {SelectedChannel.ChannelName} 最小二乘法校准成功");
            }
            else
            {
                Growl.Warning("最小二乘法校准失败，请检查输入数据");
                _log.Error("最小二乘法校准失败");
            }
        }

        [RelayCommand]
        private void AddLinearRegressionCalibrationPoint()
        {
            var point = new LinearRegressionCalibrationPoint
            {
                Index = LinearRegressionCalibrationPoints.Count + 1,
                StandardValue = LinearRegressionStandardValue,
                MeasuredValue = LinearRegressionMeasuredValue
            };
            LinearRegressionCalibrationPoints.Add(point);
            _log.Info($"添加线性回归校准点: 标准值={point.StandardValue}, 测量值={point.MeasuredValue}");
        }

        [RelayCommand]
        private void RemoveLinearRegressionCalibrationPoint(LinearRegressionCalibrationPoint? point)
        {
            if (point == null)
            {
                Growl.Warning("请先选择要删除的线性回归校准点");
                return;
            }

            LinearRegressionCalibrationPoints.Remove(point);
            for (int i = 0; i < LinearRegressionCalibrationPoints.Count; i++)
            {
                LinearRegressionCalibrationPoints[i].Index = i + 1;
            }

            SelectedLinearRegressionCalibrationPoint = LinearRegressionCalibrationPoints.FirstOrDefault();
        }

        [RelayCommand]
        private void ClearLinearRegressionCalibrationPoints()
        {
            LinearRegressionCalibrationPoints.Clear();
        }

        [RelayCommand]
        private async Task ExecuteLinearRegressionCalibration()
        {
            if (SelectedChannel == null)
            {
                Growl.Warning("请先选择通道");
                return;
            }

            if (LinearRegressionCalibrationPoints.Count < 2)
            {
                Growl.Warning("线性回归校准至少需要2个校准点");
                return;
            }

            var points = LinearRegressionCalibrationPoints.Select(p => (p.StandardValue, p.MeasuredValue)).ToList();
            var result = await _calibrationService.CalibrateLinearRegressionAsync(SelectedChannel, points);

            if (result.Success)
            {
                Growl.Info($"线性回归校准成功！\n线性系数 A = {result.CoefficientA:F6}\n线性系数 B = {result.CoefficientB:F6}\n校准公式: y = {result.CoefficientA:F6} * x + {result.CoefficientB:F6}");
                LoadCalibrationHistory();
                _log.Info($"通道 {SelectedChannel.ChannelName} 线性回归校准成功");
            }
            else
            {
                Growl.Warning("线性回归校准失败，请检查输入数据");
                _log.Error("线性回归校准失败");
            }
        }

        [RelayCommand]
        private void RefreshCalibrationHistory()
        {
            LoadCalibrationHistory();
        }

        [RelayCommand]
        private void RemoveSelectedCalibrationHistory()
        {
            if (SelectedChannel == null)
            {
                Growl.Warning("请先选择通道");
                return;
            }

            if (SelectedCalibrationHistory == null)
            {
                Growl.Warning("请先选择要删除的校准历史");
                return;
            }

            SelectedChannel.CalibrationHistory.Remove(SelectedCalibrationHistory);
            CalibrationHistory.Remove(SelectedCalibrationHistory);
            SelectedCalibrationHistory = CalibrationHistory.FirstOrDefault();
            Growl.Success("校准历史已删除");
        }

        [RelayCommand]
        private async Task SaveCalibration()
        {
            if (CurrentRecipe == null)
            {
                Growl.Warning("请先选择一个配方");
                return;
            }
            if (SelectedChannel == null)
            {
                Growl.Error("请先选中通道");
                return;
            }

            var success = await _calibrationService.SaveCalibrationAsync(SelectedChannel);
            if (success)
            {
                // 校准系数在通道对象上，保存配方文件使其持久化
                var saved = await _recipeConfigService.SaveCurrentRecipeAsync();
                if (saved)
                {
                    Growl.Success("配方保存成功"); ;
                    _log.Info("配方保存成功");
                }
                else
                {
                    Growl.Warning("配方文件保存失败");
                    _log.Warn("配方文件失败");
                }
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            if (IsLeastSquaresCalibration)
            {
                LeastSquaresCalibrationPoints.Clear();
            }
            else if (IsLinearRegressionCalibration)
            {
                LinearRegressionCalibrationPoints.Clear();
            }

            Growl.Error(string.Empty);
        }
    }
}
