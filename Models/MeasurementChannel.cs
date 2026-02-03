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
        /// 关联的PLC设备ID
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PlcDeviceName))]
        private string plcDeviceId = string.Empty;

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
                if (string.IsNullOrEmpty(PlcDeviceId))
                    return string.Empty;

                // 这里简化实现，假设ViewModel会在需要时赋值
                // 或者通过转换器处理
                return PlcDeviceId; // 临时返回ID，实际应通过ViewModel设置
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
    }

    /// <summary>
    /// 测量结果枚举
    /// </summary>
    public enum MeasurementResult
    {
        /// <summary>
        /// 未测量
        /// </summary>
        NotMeasured,

        /// <summary>
        /// 合格
        /// </summary>
        Pass,

        /// <summary>
        /// 不合格
        /// </summary>
        Fail
    }
}
