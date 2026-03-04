using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services;
using MeasurementSoftware.Services.Config;
using MeasurementSoftware.Services.Logs;
using MultiProtocol.Model;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MeasurementSoftware.ViewModels
{
    /// <summary>
    /// 校准方式
    /// </summary>
    public enum CalibrationMode
    {
        /// <summary>
        /// 单点校准（偏移校准）
        /// </summary>
        [Description("单点校准（偏移校准）")]
        SinglePoint,

        /// <summary>
        /// 多点校准（线性拟合）
        /// </summary>
        [Description("多点校准（线性拟合）")]
        MultiPoint
    }

    public partial class CalibrationViewModel : ObservableViewModel
    {
        private readonly ILog _log;
        private readonly IRecipeConfigService _recipeConfigService;
        private readonly IDeviceConfigService _deviceConfigService;
        private readonly ICalibrationService _calibrationService;

        [ObservableProperty]
        private string currentRecipeName = "未加载配方";

        public IEnumerable<MeasurementChannel> Channels => _recipeConfigService.CurrentRecipe?.Channels?.Where(c => c.IsEnabled) ?? Enumerable.Empty<MeasurementChannel>();

        [ObservableProperty]
        private MeasurementChannel? selectedChannel;

        /// <summary>
        /// 校准方式列表（供 ComboBox 绑定）
        /// </summary>
        public List<CalibrationMode> CalibrationModes { get; } =
            [CalibrationMode.SinglePoint, CalibrationMode.MultiPoint];

        /// <summary>
        /// 当前选中的校准方式
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsSinglePointCalibration))]
        private CalibrationMode selectedCalibrationMode = CalibrationMode.SinglePoint;

        /// <summary>
        /// 是否单点校准（兼容旧绑定 + TabControl 可见性）
        /// </summary>
        public bool IsSinglePointCalibration => SelectedCalibrationMode == CalibrationMode.SinglePoint;

        [ObservableProperty]
        private double singlePointStandardValue;

        [ObservableProperty]
        private double singlePointMeasuredValue;

        [ObservableProperty]
        private double multiPointStandardValue;

        [ObservableProperty]
        private double multiPointMeasuredValue;

        [ObservableProperty]
        private ObservableCollection<CalibrationPointModel> calibrationPoints = new();

        [ObservableProperty]
        private ObservableCollection<CalibrationRecord> calibrationHistory = new();



        [ObservableProperty]
        private string calibrationStatus = "--";

        [ObservableProperty]
        private string calibrationStatusColor = "#666";

        private ObservableCollection<MeasurementChannel>? _channels;

        public CalibrationViewModel(
            ILog log,
            IRecipeConfigService recipeConfigService,
            IDeviceConfigService deviceConfigService,
            ICalibrationService calibrationService)
        {
            _log = log;
            _recipeConfigService = recipeConfigService;
            _deviceConfigService = deviceConfigService;
            _calibrationService = calibrationService;

            if (_recipeConfigService is System.ComponentModel.INotifyPropertyChanged notifyPropertyChanged)
            {
                notifyPropertyChanged.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(IRecipeConfigService.CurrentRecipe))
                    {
                        BindChannels();
                        CurrentRecipeName = _recipeConfigService.CurrentRecipe?.RecipeName ?? "未加载配方";
                        OnPropertyChanged(nameof(Channels));
                    }
                };
            }

            BindChannels();
            CurrentRecipeName = _recipeConfigService.CurrentRecipe?.RecipeName ?? "未加载配方";
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
                UpdateCalibrationStatus();
                LoadCalibrationHistory();
            }
        }

        private void UpdateCalibrationStatus()
        {
            if (SelectedChannel == null) return;

            if (!SelectedChannel.RequiresCalibration)
            {
                CalibrationStatus = "无需校准";
                CalibrationStatusColor = "#666";
            }
            else if (_calibrationService.CheckCalibrationValidity(SelectedChannel))
            {
                CalibrationStatus = "校准有效";
                CalibrationStatusColor = "#4CAF50";
            }
            else
            {
                CalibrationStatus = "需要校准";
                CalibrationStatusColor = "#F44336";
            }
        }

        private async void LoadCalibrationHistory()
        {
            if (SelectedChannel == null) return;

            var history = await _calibrationService.GetCalibrationHistoryAsync(
                SelectedChannel.ChannelNumber.ToString());
            CalibrationHistory = new ObservableCollection<CalibrationRecord>(history);
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
            else
            {
                MultiPointMeasuredValue = value.Value;
            }

            _log.Info($"读取通道 {SelectedChannel.ChannelName} 当前值: {value.Value}");
        }

        /// <summary>
        /// 直接从通道绑定的PLC数据点获取当前值
        /// </summary>
        private double? GetChannelCurrentValue(MeasurementChannel channel)
        {
            if (channel.PlcDeviceId == 0 || string.IsNullOrEmpty(channel.DataPointId))
                return null;

            var device = _deviceConfigService.Devices
                .FirstOrDefault(d => d.DeviceId == channel.PlcDeviceId);
            if (device == null) return null;

            var dataPoint = device.DataPoints
                .FirstOrDefault(dp => dp.PointId == channel.DataPointId);
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
                UpdateCalibrationStatus();
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
        private void AddCalibrationPoint()
        {
            var point = new CalibrationPointModel
            {
                Index = CalibrationPoints.Count + 1,
                StandardValue = MultiPointStandardValue,
                MeasuredValue = MultiPointMeasuredValue
            };
            CalibrationPoints.Add(point);
            _log.Info($"添加校准点: 标准值={point.StandardValue}, 测量值={point.MeasuredValue}");
        }

        [RelayCommand]
        private void RemoveCalibrationPoint(CalibrationPointModel point)
        {
            CalibrationPoints.Remove(point);
            // 重新编号
            for (int i = 0; i < CalibrationPoints.Count; i++)
            {
                CalibrationPoints[i].Index = i + 1;
            }
        }

        [RelayCommand]
        private void ClearCalibrationPoints()
        {
            CalibrationPoints.Clear();
        }

        [RelayCommand]
        private async Task ExecuteMultiPointCalibration()
        {
            if (SelectedChannel == null)
            {
                Growl.Warning("请先选择通道");
                return;
            }

            if (CalibrationPoints.Count < 2)
            {
                Growl.Warning("多点校准至少需要2个校准点");
                return;
            }

            var points = CalibrationPoints.Select(p => (p.StandardValue, p.MeasuredValue)).ToList();
            var result = await _calibrationService.CalibrateChannelAsync(SelectedChannel, points);

            if (result.Success)
            {
                Growl.Info($"多点校准成功！\n" + $"线性系数 A = {result.CoefficientA:F6}\n" + $"线性系数 B = {result.CoefficientB:F6}\n" + $"校准公式: y = {result.CoefficientA:F6} * x + {result.CoefficientB:F6}");
                UpdateCalibrationStatus();
                LoadCalibrationHistory();
                _log.Info($"通道 {SelectedChannel.ChannelName} 多点校准成功");
            }
            else
            {
                Growl.Warning("多点校准失败，请检查输入数据");
                _log.Error("多点校准失败");
            }
        }

        [RelayCommand]
        private void RefreshCalibrationHistory()
        {
            LoadCalibrationHistory();
        }

        [RelayCommand]
        private async Task SaveCalibration()
        {
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
                    Growl.Info("校准数据已保存到配方文件"); ;
                    _log.Info("校准数据已保存到配方文件");
                }
                else
                {
                    Growl.Error("校准数据在内存中，配方文件保存失败");
                    _log.Warn("校准数据保存到配方文件失败");
                }
            }
        }

        /// <summary>
        /// 将校准系数写入PLC
        /// </summary>
        [RelayCommand]
        private async Task WriteCalibrationToPlc()
        {
            if (SelectedChannel == null)
            {
                Growl.Warning("请先选择通道");
                return;
            }

            if (string.IsNullOrEmpty(SelectedChannel.WriteBackDataPointIdA) &&
                string.IsNullOrEmpty(SelectedChannel.WriteBackDataPointIdB))
            {
                Growl.Warning("该通道未配置校准写回点位（系数A/B）");
                return;
            }

            try
            {
                var writeResults = new List<(bool IsSuccess, string Message)>();

                // 系数A写回
                if (!string.IsNullOrEmpty(SelectedChannel.WriteBackDataPointIdA))
                {
                    var (successA, msgA) = await WriteCoeffToPlcAsync(
                        SelectedChannel.WriteBackDataPointIdA,
                        SelectedChannel.CalibrationCoefficientA, "A");
                    writeResults.Add((successA, msgA));
                }

                // 系数B写回
                if (!string.IsNullOrEmpty(SelectedChannel.WriteBackDataPointIdB))
                {
                    var (successB, msgB) = await WriteCoeffToPlcAsync(
                        SelectedChannel.WriteBackDataPointIdB,
                        SelectedChannel.CalibrationCoefficientB, "B");
                    writeResults.Add((successB, msgB));
                }

                if (writeResults.Count == 0)
                {
                    Growl.Warning("没有有效的写回点位");
                    return;
                }

                var allSuccess = writeResults.All(r => r.IsSuccess);
                if (allSuccess)
                {
                    Growl.Success($"校准系数已写入PLC\nA={SelectedChannel.CalibrationCoefficientA:F6}  B={SelectedChannel.CalibrationCoefficientB:F6}");
                    _log.Info($"通道 {SelectedChannel.ChannelName} 校准系数写入PLC成功");
                }
                else
                {
                    var errors = string.Join(", ", writeResults.Where(r => !r.IsSuccess).Select(r => r.Message));
                    Growl.Error($"写入PLC失败: {errors}");
                    _log.Error($"通道 {SelectedChannel.ChannelName} 校准系数写入PLC失败: {errors}");
                }
            }
            catch (Exception ex)
            {
                Growl.Error($"写入PLC异常: {ex.Message}");
                _log.Error($"写入PLC异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 根据 FieldType 获取对应的 CLR 类型，用于值转换
        /// </summary>
        private static Type GetClrType(FieldType fieldType) => fieldType switch
        {
            FieldType.Bool => typeof(bool),
            FieldType.Byte => typeof(byte),
            FieldType.Int16 => typeof(short),
            FieldType.UInt16 => typeof(ushort),
            FieldType.Int32 => typeof(int),
            FieldType.UInt32 => typeof(uint),
            FieldType.Int64 => typeof(long),
            FieldType.UInt64 => typeof(ulong),
            FieldType.Long => typeof(long),
            FieldType.Float => typeof(float),
            FieldType.Double => typeof(double),
            FieldType.String => typeof(string),
            FieldType.Char => typeof(char),
            _ => typeof(float)
        };

        /// <summary>
        /// 从所有设备中查找包含指定点位的设备，写入系数值
        /// </summary>
        private async Task<(bool IsSuccess, string Message)> WriteCoeffToPlcAsync(
            string pointId, double coeffValue, string coeffName)
        {
            foreach (var device in _deviceConfigService.Devices)
            {
                var dataPoint = device.DataPoints.FirstOrDefault(dp => dp.PointId == pointId);
                if (dataPoint == null) continue;

                if (device.protocol == null || !device.IsConnected)
                    return (false, $"系数{coeffName}所在设备 [{device.DeviceName}] 未连接");

                var field = new FieldInfo(dataPoint.Address, dataPoint.DataType, dataPoint.ByteOrder)
                {
                    Value = Convert.ChangeType(coeffValue, GetClrType(dataPoint.DataType))
                };

                var results = await device.protocol.WriteDataAsync(device.DeviceId, [field]);
                var result = results.FirstOrDefault();
                return result != null && result.IsSuccess
                    ? (true, string.Empty)
                    : (false, result?.Message ?? $"系数{coeffName}写入失败");
            }

            return (false, $"系数{coeffName}写回点位 {pointId} 在所有设备中均未找到");
        }

        [RelayCommand]
        private void Cancel()
        {
            CalibrationPoints.Clear();
            Growl.Error(string.Empty);
        }
    }

    // 校准点模型
    public partial class CalibrationPointModel : ObservableViewModel
    {
        [ObservableProperty]
        private int index;

        [ObservableProperty]
        private double standardValue;

        [ObservableProperty]
        private double measuredValue;
    }
}
