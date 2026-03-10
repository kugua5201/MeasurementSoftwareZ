using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.ViewModels;
using System.Collections.ObjectModel;

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
        private ChannelType channelType;

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
        /// PLC设备名称（用于显示）
        /// </summary>
        public string PlcDeviceName
        {
            get
            {
                if (PlcDeviceId == 0)
                    return string.Empty;

                return PlcDeviceId.ToString();
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
        /// 校准系数A写回PLC点位ID（关联到已配置的DataPoint）
        /// </summary>
        [ObservableProperty]
        private string writeBackDataPointIdA = string.Empty;

        /// <summary>
        /// 校准系数B写回PLC点位ID（关联到已配置的DataPoint）
        /// </summary>
        [ObservableProperty]
        private string writeBackDataPointIdB = string.Empty;

        /// <summary>
        /// 采样数量（缓存数据大小，用于计算最大值、最小值、跳动等）
        /// </summary>
        [ObservableProperty]
        private int sampleCount = 100;

        /// <summary>
        /// 历史数据（用于计算最大值、最小值等）
        /// </summary>
        public List<double> HistoricalData { get; set; } = new List<double>();

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
                HistoricalData.Add(rawValue);
                MeasuredValue = rawValue;
                // 保持缓存大小
                //if (SampleCount > 0 && HistoricalData.Count > SampleCount)
                //{
                //    HistoricalData.RemoveAt(0); // 移除最旧的数据
                //}

                //if (HistoricalData.Count > 0)
                //{
                //    double calculatedValue = rawValue;
                //    switch (ChannelType)
                //    {
                //        case ChannelType.结果值:
                //            calculatedValue = rawValue;
                //            break;
                //        case ChannelType.最大值:
                //            calculatedValue = HistoricalData.Max();
                //            break;
                //        case ChannelType.最小值:
                //            calculatedValue = HistoricalData.Min();
                //            break;
                //        case ChannelType.平均值:
                //            calculatedValue = HistoricalData.Average();
                //            break;
                //        case ChannelType.跳动值:
                //        case ChannelType.齿跳动值:
                //            calculatedValue = HistoricalData.Max() - HistoricalData.Min();
                //            break;
                //    }

                //    // 保留指定小数位数
                //    // StandardValue = Math.Round(calculatedValue, DecimalPlaces);
                //    //读取的原始测量值
                //    MeasuredValue = rawValue;
                //}

                // 进行预判
                //CheckResult();
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
        /// 检查校准是否过期
        /// </summary>
        public bool IsCalibrationExpired()
        {
            if (!RequiresCalibration || !LastCalibrationTime.HasValue)
                return false;

            var expiryDate = LastCalibrationTime.Value.AddDays(CalibrationValidityDays);
            return DateTime.Now > expiryDate;
        }


        /// <summary>
        /// 最终结果
        /// </summary>
        [ObservableProperty]
        private double reusltValue;


        //更新最终结果值
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


            CheckResult();
        }

    }


}
