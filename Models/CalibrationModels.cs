using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MeasurementSoftware.Models
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
        /// 最小二乘法校准
        /// </summary>
        [Description("最小二乘法校准")]
        LeastSquares,

        /// <summary>
        /// 线性回归校准
        /// </summary>
        [Description("线性回归校准")]
        LinearRegression
    }

    /// <summary>
    /// 单点校准配置
    /// </summary>
    public partial class SinglePointCalibrationSettings : ObservableViewModel
    {
        [ObservableProperty]
        private double standardValue;

        [ObservableProperty]
        private double measuredValue;

        [ObservableProperty]
        private double coefficientA = 1.0;

        [ObservableProperty]
        private double coefficientB;

        [ObservableProperty]
        private DateTime? lastCalibrationTime;
    }

    /// <summary>
    /// 最小二乘法校准点
    /// </summary>
    public partial class LeastSquaresCalibrationPoint : ObservableViewModel
    {
        [ObservableProperty]
        private int index;

        [ObservableProperty]
        private double standardValue;

        [ObservableProperty]
        private double measuredValue;
    }

    /// <summary>
    /// 最小二乘法校准配置
    /// </summary>
    public partial class LeastSquaresCalibrationSettings : ObservableViewModel
    {
        [ObservableProperty]
        private double inputStandardValue;

        [ObservableProperty]
        private double inputMeasuredValue;

        [ObservableProperty]
        private ObservableCollection<LeastSquaresCalibrationPoint> points = [];

        [ObservableProperty]
        private double coefficientA = 1.0;

        [ObservableProperty]
        private double coefficientB;

        [ObservableProperty]
        private DateTime? lastCalibrationTime;
    }

    /// <summary>
    /// 线性回归校准点
    /// </summary>
    public partial class LinearRegressionCalibrationPoint : ObservableViewModel
    {
        [ObservableProperty]
        private int index;

        [ObservableProperty]
        private double standardValue;

        [ObservableProperty]
        private double measuredValue;
    }

    /// <summary>
    /// 线性回归校准配置
    /// </summary>
    public partial class LinearRegressionCalibrationSettings : ObservableViewModel
    {
        [ObservableProperty]
        private double inputStandardValue;

        [ObservableProperty]
        private double inputMeasuredValue;

        [ObservableProperty]
        private ObservableCollection<LinearRegressionCalibrationPoint> points = [];

        [ObservableProperty]
        private double coefficientA = 1.0;

        [ObservableProperty]
        private double coefficientB;

        [ObservableProperty]
        private DateTime? lastCalibrationTime;
    }

    /// <summary>
    /// 校准记录点
    /// </summary>
    public class CalibrationRecordPoint
    {
        public double StandardValue { get; set; }
        public double MeasuredValue { get; set; }
    }

    /// <summary>
    /// 校准记录
    /// </summary>
    public class CalibrationRecord
    {
        public string RecordId { get; set; } = Guid.NewGuid().ToString();
        public string ChannelId { get; set; } = string.Empty;
        public DateTime CalibrationTime { get; set; } = DateTime.Now;
        public CalibrationMode Mode { get; set; }
        public string MethodName { get; set; } = string.Empty;
        public double CoefficientA { get; set; }
        public double CoefficientB { get; set; }
        public string OperatorName { get; set; } = string.Empty;
        public List<CalibrationRecordPoint> CalibrationPoints { get; set; } = [];
    }
}
