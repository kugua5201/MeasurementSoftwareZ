using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace MeasurementSoftware.Models
{
    /// <summary>
    /// 测量通道模型
    /// </summary>
    public partial class MeasurementChannel : ObservableViewModel
    {
        /// <summary>
        /// 通道编号
        /// </summary>
        [ObservableProperty]
        private int channelNumber;

        /// <summary>
        /// 通道名称
        /// </summary>
        [ObservableProperty]
        private string channelName = string.Empty;

        /// <summary>
        /// 通道说明
        /// </summary>
        [ObservableProperty]
        private string channelDescription = string.Empty;

        /// <summary>
        /// 通道类型
        /// </summary>
        [ObservableProperty]
        private ChannelType channelType = ChannelType.结果值;

        /// <summary>
        /// 测量类型
        /// </summary>
        [ObservableProperty]
        private string measurementType = string.Empty;

        /// <summary>
        /// 标准值
        /// </summary>
        [ObservableProperty]
        private double standardValue;

        /// <summary>
        /// 公差上限
        /// </summary>
        [ObservableProperty]
        private double upperTolerance;

        /// <summary>
        /// 公差下限
        /// </summary>
        [ObservableProperty]
        private double lowerTolerance;


        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayMeasuredValue))]
        private double measuredValue;

        partial void OnMeasuredValueChanging(double value)
        {
            measuredValue = Math.Round(value, DecimalPlaces);
        }

        /// <summary>
        /// 测量结果（合格/不合格）
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayResultText))]
        private MeasurementResult result;

        /// <summary>
        /// 数据源地址（PLC地址）
        /// </summary>
        [ObservableProperty]
        private string dataSourceAddress = string.Empty;

        /// <summary>
        /// 关联的PLC设备ID（0表示未关联）
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PlcDeviceName))]
        private long plcDeviceId;

        /// <summary>
        /// 关联的数据点ID
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DataPointName))]
        private string dataPointId = string.Empty;

        /// <summary>
        /// 可用的数据点列表（根据选择的PLC设备动态加载）
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DataPointName))]
        private ObservableCollection<DataPoint> availableDataPoints = new();

        /// <summary>
        /// 运行时绑定的 PLC 设备实例。
        /// 仅在程序运行期使用，不参与配方持久化。
        /// </summary>
        private PlcDevice? runtimeDevice;
        private PropertyChangedEventHandler? runtimeDevicePropertyChangedHandler;

        [JsonIgnore]
        public PlcDevice? RuntimeDevice
        {
            get => runtimeDevice;
            set
            {
                var oldDevice = runtimeDevice;
                if (ReferenceEquals(runtimeDevice, value))
                {
                    return;
                }

                if (oldDevice != null && runtimeDevicePropertyChangedHandler != null)
                {
                    oldDevice.PropertyChanged -= runtimeDevicePropertyChangedHandler;
                }

                runtimeDevice = value;

                if (oldDevice != null && oldDevice.DeviceId != value?.DeviceId)
                {
                    RuntimeDataPoint = null;
                    UseCacheValue = false;
                }

                PlcDeviceId = value?.DeviceId ?? 0;
                RefreshAvailableDataPoints();

                if (runtimeDataPoint == null || !AvailableDataPoints.Contains(runtimeDataPoint))
                {
                    RuntimeDataPoint = AvailableDataPoints.FirstOrDefault(dp => dp.PointId == DataPointId);
                }

                if (runtimeDevice != null)
                {
                    runtimeDevicePropertyChangedHandler = RuntimeDevice_PropertyChanged;
                    runtimeDevice.PropertyChanged += runtimeDevicePropertyChangedHandler;
                }

                OnPropertyChanged(nameof(RuntimeDevice));
                OnPropertyChanged(nameof(PlcDeviceName));
                OnPropertyChanged(nameof(DataPointName));
            }
        }

        /// <summary>
        /// 运行时绑定的采集点位实例。
        /// 仅在程序运行期使用，不参与配方持久化。
        /// </summary>
        private DataPoint? runtimeDataPoint;
        private PropertyChangedEventHandler? runtimeDataPointPropertyChangedHandler;

        [JsonIgnore]
        public DataPoint? RuntimeDataPoint
        {
            get => runtimeDataPoint;
            set
            {
                if (ReferenceEquals(runtimeDataPoint, value))
                {
                    return;
                }

                if (runtimeDataPoint != null && runtimeDataPointPropertyChangedHandler != null)
                {
                    runtimeDataPoint.PropertyChanged -= runtimeDataPointPropertyChangedHandler;
                }

                runtimeDataPoint = value;
                DataPointId = value?.PointId ?? string.Empty;
                DataSourceAddress = value?.Address ?? string.Empty;

                if (runtimeDataPoint != null)
                {
                    runtimeDataPointPropertyChangedHandler = RuntimeDataPoint_PropertyChanged;
                    runtimeDataPoint.PropertyChanged += runtimeDataPointPropertyChangedHandler;
                }

                OnPropertyChanged(nameof(RuntimeDataPoint));
                OnPropertyChanged(nameof(DataPointName));
            }
        }

        /// <summary>
        /// 按已保存的设备/点位标识回填运行时绑定。
        /// 仅刷新运行时引用，不修改持久化的设备、点位与地址字段。
        /// </summary>
        public void HydrateRuntimeBindings(PlcDevice? device)
        {
            if (runtimeDevice != null && runtimeDevicePropertyChangedHandler != null)
            {
                runtimeDevice.PropertyChanged -= runtimeDevicePropertyChangedHandler;
            }

            if (runtimeDataPoint != null && runtimeDataPointPropertyChangedHandler != null)
            {
                runtimeDataPoint.PropertyChanged -= runtimeDataPointPropertyChangedHandler;
            }

            runtimeDevice = device;

            AvailableDataPoints = runtimeDevice == null
                ? []
                : new ObservableCollection<DataPoint>(runtimeDevice.DataPoints
                    .Where(dp => dp.IsEnabled)
                    .OrderBy(dp => int.TryParse(dp.PointId, out var id) ? id : int.MaxValue));

            runtimeDataPoint = AvailableDataPoints.FirstOrDefault(dp => dp.PointId == DataPointId);

            if (runtimeDevice != null)
            {
                runtimeDevicePropertyChangedHandler = RuntimeDevice_PropertyChanged;
                runtimeDevice.PropertyChanged += runtimeDevicePropertyChangedHandler;
            }

            if (runtimeDataPoint != null)
            {
                runtimeDataPointPropertyChangedHandler = RuntimeDataPoint_PropertyChanged;
                runtimeDataPoint.PropertyChanged += runtimeDataPointPropertyChangedHandler;
                DataSourceAddress = runtimeDataPoint.Address;
            }

            OnPropertyChanged(nameof(RuntimeDevice));
            OnPropertyChanged(nameof(RuntimeDataPoint));
            OnPropertyChanged(nameof(PlcDeviceName));
            OnPropertyChanged(nameof(DataPointName));
        }

        /// <summary>
        /// PLC设备名称（用于显示）
        /// </summary>
        public string PlcDeviceName
        {
            get
            {
                if (PlcDeviceId == 0)
                    return string.Empty;

                return RuntimeDevice?.DeviceName ?? PlcDeviceId.ToString();
            }
        }

        /// <summary>
        /// 数据点名称（用于显示）
        /// </summary>
        public string DataPointName
        {
            get
            {
                if (string.IsNullOrEmpty(DataPointId))
                    return string.Empty;

                if (RuntimeDataPoint != null)
                    return RuntimeDataPoint.PointName;

                // 从可用数据点列表中查找点位名称
                var point = AvailableDataPoints?.FirstOrDefault(p => p.PointId == DataPointId);
                return point?.PointName ?? DataPointId;
            }
        }

        /// <summary>
        /// 是否启用
        /// </summary>
        [ObservableProperty]
        private bool isEnabled = true;

        /// <summary>
        /// 单位
        /// </summary>
        [ObservableProperty]
        private string unit = string.Empty;

        /// <summary>
        /// 小数位数
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayMeasuredValue))]
        [NotifyPropertyChangedFor(nameof(DisplayResultValue))]
        private int decimalPlaces = 3;

        /// <summary>
        /// 是否需要校准
        /// </summary>
        [ObservableProperty]
        private bool requiresCalibration;

        /// <summary>
        /// 当前生效的校准方式
        /// </summary>
        [ObservableProperty]
        private CalibrationMode calibrationMode = CalibrationMode.SinglePoint;

        /// <summary>
        /// 校准系数 A（线性公式：y = Ax + B）
        /// </summary>
        [ObservableProperty]
        private double calibrationCoefficientA = 1.0;

        /// <summary>
        /// 校准系数 B（线性公式：y = Ax + B）
        /// </summary>
        [ObservableProperty]
        private double calibrationCoefficientB = 0.0;

        /// <summary>
        /// 上次校准时间
        /// </summary>
        [ObservableProperty]
        private DateTime? lastCalibrationTime;


        /// <summary>
        /// 单点校准配置
        /// </summary>
        [ObservableProperty]
        private SinglePointCalibrationSettings singlePointCalibration = new();

        /// <summary>
        /// 最小二乘法校准配置
        /// </summary>
        [ObservableProperty]
        private LeastSquaresCalibrationSettings leastSquaresCalibration = new();

        /// <summary>
        /// 线性回归校准配置
        /// </summary>
        [ObservableProperty]
        private LinearRegressionCalibrationSettings linearRegressionCalibration = new();

        /// <summary>
        /// 校准历史（随配方保存）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<CalibrationRecord> calibrationHistory = [];

        /// <summary>
        /// 工步编号（用于分步测量）
        /// </summary>
        [ObservableProperty]
        private int stepNumber = 1;

        /// <summary>
        /// 工步名称
        /// </summary>
        [ObservableProperty]
        private string stepName = "默认工步";

        /// <summary>
        /// 通道标注点（在产品图片上标注测量位置，每个通道最多一个标注）
        /// </summary>
        [ObservableProperty]
        private ChannelAnnotation? annotation;

        /// <summary>
        /// 是否使用缓存值（仅适用于 S7-1200/1500 启用缓存的点位）
        /// true = 读取缓存解析值，false = 读取寄存器实时值
        /// </summary>
        [ObservableProperty]
        private bool useCacheValue;

        /// <summary>
        /// 采样数量（缓存数据大小，用于计算最大值、最小值、跳动等）
        /// </summary>
        [ObservableProperty]
        private int sampleCount = 100;

        /// <summary>
        /// 实时值是否有效。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayMeasuredValue))]
        private bool isMeasuredValueAvailable;

        [JsonIgnore]
        /// <summary>
        /// 历史数据（用于计算最大值、最小值等）
        /// </summary>
        public List<double> HistoricalData { get; set; } = [];

        /// <summary>
        /// 绑定运行时设备实例，并同步可用点位集合。
        /// </summary>
        public void BindDevice(PlcDevice? device)
        {
            RuntimeDevice = device;
        }

        /// <summary>
        /// 绑定运行时采集点位实例。
        /// </summary>
        public void BindDataPoint(DataPoint? dataPoint)
        {
            RuntimeDataPoint = dataPoint;
        }

        /// <summary>
        /// 清空运行时设备与点位绑定。
        /// </summary>
        public void ClearRuntimeBindings()
        {
            RuntimeDevice = null;
            RuntimeDataPoint = null;
            PlcDeviceId = 0;
            DataPointId = string.Empty;
            DataSourceAddress = string.Empty;
            AvailableDataPoints = [];
            UseCacheValue = false;
            OnPropertyChanged(nameof(PlcDeviceName));
            OnPropertyChanged(nameof(DataPointName));
        }

        private void RuntimeDevice_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlcDevice.DeviceName))
            {
                OnPropertyChanged(nameof(PlcDeviceName));
            }
        }

        private void RuntimeDataPoint_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(DataPoint.PointId) or nameof(DataPoint.PointName) or nameof(DataPoint.Address))
            {
                if (sender is DataPoint point)
                {
                    DataPointId = point.PointId;
                    DataSourceAddress = point.Address;
                }

                OnPropertyChanged(nameof(DataPointName));
            }
        }

        /// <summary>
        /// 根据当前绑定设备刷新可用点位列表，并回填运行时点位引用。
        /// </summary>
        public void RefreshAvailableDataPoints()
        {
            AvailableDataPoints = RuntimeDevice == null ? [] : new ObservableCollection<DataPoint>(RuntimeDevice.DataPoints
                    .Where(dp => dp.IsEnabled)
                    .OrderBy(dp => int.TryParse(dp.PointId, out var id) ? id : int.MaxValue));

            RuntimeDataPoint = AvailableDataPoints.FirstOrDefault(dp => dp.PointId == DataPointId);
            if (RuntimeDataPoint != null)
            {
                DataSourceAddress = RuntimeDataPoint.Address;
            }

            OnPropertyChanged(nameof(DataPointName));
        }

        /// <summary>
        /// 检查测量结果是否合格
        /// </summary>
        public void CheckResult()
        {
            var upperLimit = StandardValue + UpperTolerance;
            var lowerLimit = StandardValue - LowerTolerance;
            var valueToCheck = IsResultValueAvailable ? ReusltValue : MeasuredValue;

            if (valueToCheck >= lowerLimit && valueToCheck <= upperLimit)
            {
                Result = MeasurementResult.Pass;
            }
            else
            {
                Result = MeasurementResult.Fail;
            }
        }

        private readonly Lock _dataLock = new();

        /// <summary>
        /// 根据通道类型处理并更新测量值
        /// </summary>
        /// <param name="rawValue">原始值</param>
        public void UpdateMeasuredValue(double rawValue)
        {
            lock (_dataLock)
            {
                IsMeasuredValueAvailable = true;
                MeasuredValue = rawValue;
                var checkValue = RoundMeasuredValue(rawValue);
                TrimHistoricalDataForIncoming(SampleCount, 1);
                HistoricalData.Add(checkValue);

            }
        }

        /// <summary>
        /// 硬件缓存时加入硬件缓存值，并且刷新实时值
        /// </summary>
        /// <param name="rawValues"></param>
        /// <param name="rawValue"></param>
        public void AppendMeasuredValues(IEnumerable<double> rawValues, double rawValue)
        {
            lock (_dataLock)
            {
                IsMeasuredValueAvailable = true;
                MeasuredValue = rawValue;
                if (rawValues is IReadOnlyList<double> rawValueList)
                {
                    int startIndex = Math.Max(0, rawValueList.Count - SampleCount);
                    int incomingCount = rawValueList.Count - startIndex;
                    TrimHistoricalDataForIncoming(SampleCount, incomingCount);
                    int requiredCapacity = HistoricalData.Count + incomingCount;
                    if (HistoricalData.Capacity < requiredCapacity)
                    {
                        HistoricalData.Capacity = requiredCapacity;
                    }
                    for (int i = startIndex; i < rawValueList.Count; i++)
                    {
                        double lastMeasuredValue = RoundMeasuredValue(rawValueList[i]);
                        HistoricalData.Add(lastMeasuredValue);
                    }
                }

            }
        }

        /// <summary>
        /// 去掉历史数据
        /// </summary>
        private void TrimHistoricalData()
        {
            int maxSamples = Math.Max(1, SampleCount);
            if (HistoricalData.Count > maxSamples)
            {
                HistoricalData.RemoveRange(0, HistoricalData.Count - maxSamples);
            }
        }

        /// <summary>
        /// 去掉历史数据中超出容量限制的旧数据，以便为即将追加的新数据腾出空间。
        /// </summary>
        /// <param name="maxSamples">历史数据的最大容量</param>
        /// <param name="incomingCount">即将追加的新数据数量</param>
        private void TrimHistoricalDataForIncoming(int maxSamples, int incomingCount)
        {
            if (incomingCount >= maxSamples)
            {
                HistoricalData.Clear();
                return;
            }

            int overflow = HistoricalData.Count + incomingCount - maxSamples;
            if (overflow > 0)
            {
                HistoricalData.RemoveRange(0, overflow);
            }
        }

        /// <summary>
        /// 通过校准并且转换保留对应的值
        /// </summary>
        /// <param name="rawValue">原始测量值</param>
        /// <param name="applyCalibration">是否应用校准</param>
        /// <returns>经过校准和小数位处理后的测量值</returns>
        private double RoundMeasuredValue(double rawValue)
        {
            if (RequiresCalibration)
            {
                rawValue = CalibrationCoefficientA * rawValue + CalibrationCoefficientB;
            }

            return Math.Round(rawValue, DecimalPlaces);
        }


        /// <summary>
        /// 最终结果
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayResultValue))]
        private double reusltValue;

        /// <summary>
        /// 结果值是否有效。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayResultValue))]
        private bool isResultValueAvailable;

        /// <summary>
        /// 通道显示状态。
        /// </summary>
        [ObservableProperty]
        private MeasurementResult displayState = MeasurementResult.Waiting;

        [JsonIgnore]
        public string DisplayMeasuredValue => IsMeasuredValueAvailable ? MeasuredValue.ToString($"F{Math.Max(0, DecimalPlaces)}") : "----";

        [JsonIgnore]
        public string DisplayResultValue => IsResultValueAvailable ? ReusltValue.ToString($"F{Math.Max(0, DecimalPlaces)}") : "----";

        [JsonIgnore]
        public string DisplayResultText => Result switch
        {
            MeasurementResult.Pass => "OK",
            MeasurementResult.Fail => "NG",
            _ => "--"
        };


        /// <summary>
        /// 更新最终结果值
        /// </summary>
        public void UpdateResultValue()
        {
            if (HistoricalData == null || HistoricalData.Count == 0)
            {
                ReusltValue = 0;
                IsResultValueAvailable = false;
                ChannelDescription = "没有采集到数据";
                Result = MeasurementResult.Fail;
                return;
            }
            switch (ChannelType)
            {
                case ChannelType.结果值:
                    ReusltValue = MeasuredValue;
                    break;
                case ChannelType.最大值:
                    ReusltValue = HistoricalData.Max();
                    break;
                case ChannelType.最小值:
                    ReusltValue = HistoricalData.Min();
                    break;
                case ChannelType.平均值:
                    ReusltValue = HistoricalData.Average();
                    break;
                case ChannelType.跳动值:
                case ChannelType.齿跳动值:
                    ReusltValue = HistoricalData.Max() - HistoricalData.Min();
                    break;
            }

            ReusltValue = Math.Round(ReusltValue, DecimalPlaces);
            IsResultValueAvailable = true;
            CheckResult();
        }

        public void ResetMeasurementState()
        {
            MeasuredValue = 0;
            ReusltValue = 0;
            Result = MeasurementResult.NotMeasured;
            DisplayState = MeasurementResult.Waiting;
            IsMeasuredValueAvailable = false;
            IsResultValueAvailable = false;
            ChannelDescription = string.Empty;
            HistoricalData.Clear();
        }

        public void SetDisplayStateFromResult()
        {
            DisplayState = Result switch
            {
                MeasurementResult.Pass => MeasurementResult.Pass,
                MeasurementResult.Fail => MeasurementResult.Fail,
                _ => MeasurementResult.Waiting
            };
        }

    }


}
