using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.ViewModels;
using System.Collections.ObjectModel;
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

        /// <summary>
        /// 测量值
        /// </summary>
        [ObservableProperty]
        private double measuredValue;

        /// <summary>
        /// 测量结果（合格/不合格）
        /// </summary>
        [ObservableProperty]
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

                runtimeDataPoint = value;
                DataPointId = value?.PointId ?? string.Empty;
                DataSourceAddress = value?.Address ?? string.Empty;
                OnPropertyChanged(nameof(RuntimeDataPoint));
                OnPropertyChanged(nameof(DataPointName));
            }
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
        /// 校准有效期（天）
        /// </summary>
        [ObservableProperty]
        private int calibrationValidityDays = 30;

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

        /// <summary>
        /// 根据当前绑定设备刷新可用点位列表，并回填运行时点位引用。
        /// </summary>
        public void RefreshAvailableDataPoints()
        {
            AvailableDataPoints = RuntimeDevice == null
                ? []
                : new ObservableCollection<DataPoint>(RuntimeDevice.DataPoints
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

            if (MeasuredValue >= lowerLimit && MeasuredValue <= upperLimit)
            {
                Result = MeasurementResult.Pass;
            }
            else
            {
                Result = MeasurementResult.Fail;
            }
        }

        private readonly object _dataLock = new object();

        /// <summary>
        /// 根据通道类型处理并更新测量值
        /// </summary>
        /// <param name="rawValue">原始值</param>
        public void UpdateMeasuredValue(double rawValue)
        {
            lock (_dataLock)
            {
                //保留对应小数
                MeasuredValue =Math.Round (rawValue, DecimalPlaces);
                // 保持缓存大小
                if (HistoricalData.Count > SampleCount)
                {
                    while (HistoricalData.Count > SampleCount)
                    {
                        HistoricalData.RemoveAt(0);
                    }
                }
                HistoricalData.Add(MeasuredValue);
            }
        }

        /// <summary>
        /// 应用校准（线性匹配）
        /// </summary>
        public double ApplyCalibration(double rawValue)
        {
            if (RequiresCalibration)
            {
                return CalibrationCoefficientA * rawValue + CalibrationCoefficientB;
            }
            return rawValue;
        }


        /// <summary>
        /// 最终结果
        /// </summary>
        [ObservableProperty]
        private double reusltValue;


        /// <summary>
        /// 更新最终结果值
        /// </summary>
        public void UpdateResultValue()
        {
            if (HistoricalData == null || HistoricalData.Count == 0)
            {
                ReusltValue = 0;
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
            CheckResult();
        }

    }


}
