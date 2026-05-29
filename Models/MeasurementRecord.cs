using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.Extensions;
using MeasurementSoftware.ViewModels;
using System.Globalization;

namespace MeasurementSoftware.Models
{


    /// <summary>
    /// 测量记录模型
    /// </summary>
    public partial class MeasurementRecord : ObservableViewModel
    {
        /// <summary>
        /// 记录ID
        /// </summary>
        [ObservableProperty]
        private string recordId = Guid.NewGuid().ToString();

        /// <summary>
        /// 配方ID
        /// </summary>
        [ObservableProperty]
        private string recipeId = string.Empty;

        /// <summary>
        /// 配方名称
        /// </summary>
        [ObservableProperty]
        private string recipeName = string.Empty;

        /// <summary>
        /// 测量时间
        /// </summary>
        [ObservableProperty]
        private DateTime measurementTime = DateTime.Now;

        /// <summary>
        /// 测量结果
        /// </summary>
        [ObservableProperty]
        private MeasurementResult overallResult;

        public string OverallResultTxt => OverallResult.GetDescription();

        /// <summary>
        /// 检测记录页显示用顺序号。
        /// </summary>
        [ObservableProperty]
        private int displayIndex;

        /// <summary>
        /// 是否启用工步显示文本。
        /// </summary>
        public string IsStepMeasurementText => IsStepMeasurement ? "是" : "否";

        /// <summary>
        /// 工步数显示文本。
        /// 启用工步时显示“当前工步/总工步”。
        /// </summary>
        public string StepDisplay => IsStepMeasurement ? $"{StepNumber}/{TotalSteps}" : "-";
        /// <summary>
        /// 本次测量中 OK 通道数量。
        /// </summary>
        [ObservableProperty]
        private int passChannelCount;

        /// <summary>
        /// 本次测量中 NG 通道数量。
        /// </summary>
        [ObservableProperty]
        private int failChannelCount;

        /// <summary>
        /// 通道测量数据
        /// </summary>
        public List<ChannelMeasurementData> ChannelData { get; set; } = new List<ChannelMeasurementData>();

        /// <summary>
        /// 操作员
        /// </summary>
        [ObservableProperty]
        private string operatorName = string.Empty;

        /// <summary>
        /// 绑定的二维码
        /// </summary>
        [ObservableProperty]
        private string barcode = string.Empty;

        /// <summary>
        /// 二维码扫描时间
        /// </summary>
        [ObservableProperty]
        private DateTime? barcodeScanTime;

        /// <summary>
        /// 工步编号（如果是分步测量）
        /// </summary>
        [ObservableProperty]
        private int stepNumber = 1;

        /// <summary>
        /// 总工步数
        /// </summary>
        [ObservableProperty]
        private int totalSteps = 1;

        /// <summary>
        /// 是否为工步测量。
        /// </summary>
        [ObservableProperty]
        private bool isStepMeasurement;

        /// <summary>
        /// MES上传状态
        /// </summary>
        [ObservableProperty]
        private UploadStatus mesUploadStatus = UploadStatus.Pending;

        /// <summary>
        /// MES上传时间
        /// </summary>
        [ObservableProperty]
        private DateTime? mesUploadTime;

        /// <summary>
        /// PLC传输状态
        /// </summary>
        [ObservableProperty]
        private UploadStatus plcTransferStatus = UploadStatus.Pending;

        /// <summary>
        /// PLC传输时间
        /// </summary>
        [ObservableProperty]
        private DateTime? plcTransferTime;

        /// <summary>
        /// 备注
        /// </summary>
        [ObservableProperty]
        private string remarks = string.Empty;
    }

    /// <summary>
    /// 通道测量数据
    /// </summary>
    public class ChannelMeasurementData
    {
        /// <summary>
        /// 通道详情显示序号。
        /// </summary>
        public int DisplayIndex { get; set; }

        /// <summary>
        /// 通道编号
        /// </summary>
        public int ChannelNumber { get; set; }

        /// <summary>
        /// 带前缀的通道编号文本。
        /// 用于数据库持久化及导出显示。
        /// </summary>
        public string ChannelNumberText { get; set; } = string.Empty;

        /// <summary>
        /// 通道名称
        /// </summary>
        public string ChannelName { get; set; } = string.Empty;

        /// <summary>
        /// 通道说明。
        /// </summary>
        public string ChannelDescription { get; set; } = string.Empty;

        /// <summary>
        /// 通道类型。
        /// </summary>
        public string ChannelType { get; set; } = string.Empty;

        /// <summary>
        /// 测量模式。
        /// 例如直接测量、间接测量、虚拟通道。
        /// </summary>
        public string MeasurementMode { get; set; } = string.Empty;

        /// <summary>
        /// 来源摘要。
        /// 用于历史记录中快速区分直测、间接、虚拟及其具体来源类型。
        /// </summary>
        public string SourceSummary { get; set; } = string.Empty;

        /// <summary>
        /// 公式脚本。
        /// 间接测量和虚拟公式通道用于还原结果计算来源。
        /// </summary>
        public string FormulaScript { get; set; } = string.Empty;

        /// <summary>
        /// 测量类型。
        /// </summary>
        public string MeasurementType { get; set; } = string.Empty;

        /// <summary>
        /// 数据源地址。
        /// </summary>
        public string DataSourceAddress { get; set; } = string.Empty;

        /// <summary>
        /// PLC设备名称。
        /// </summary>
        public string PlcDeviceName { get; set; } = string.Empty;

        /// <summary>
        /// 数据点名称。
        /// </summary>
        public string DataPointName { get; set; } = string.Empty;

        /// <summary>
        /// 是否启用。
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// 小数位数。
        /// </summary>
        public int DecimalPlaces { get; set; }

        /// <summary>
        /// 是否需要校准。
        /// </summary>
        public bool RequiresCalibration { get; set; }

        /// <summary>
        /// 校准方式。
        /// </summary>
        public CalibrationMode CalibrationMode { get; set; } = CalibrationMode.SinglePoint;

        /// <summary>
        /// 校准系数A。
        /// </summary>
        public double CalibrationCoefficientA { get; set; }

        /// <summary>
        /// 校准系数B。
        /// </summary>
        public double CalibrationCoefficientB { get; set; }

        /// <summary>
        /// 上次校准时间。
        /// </summary>
        //public DateTime? LastCalibrationTime { get; set; }


        /// <summary>
        /// 是否使用缓存值。
        /// </summary>
        public bool UseCacheValue { get; set; }

        /// <summary>
        /// 采样数量。
        /// </summary>
        public int SampleCount { get; set; }

        /// <summary>
        /// 标准值
        /// </summary>
        public double StandardValue { get; set; }

        /// <summary>
        /// 公差上限
        /// </summary>
        public double UpperTolerance { get; set; }

        /// <summary>
        /// 公差下限
        /// </summary>
        public double LowerTolerance { get; set; }

        /// <summary>
        /// 测量值
        /// </summary>
        public double MeasuredResultValue { get; set; }

        /// <summary>
        /// 单位。
        /// </summary>
        public string Unit { get; set; } = string.Empty;

        /// <summary>
        /// 通道所属工步编号。
        /// </summary>
        public int StepNumber { get; set; }

        /// <summary>
        /// 通道工步显示文本。
        /// 虚拟通道的测量通道计算模式统一显示为 --。
        /// </summary>
        public string StepDisplayText { get; set; } = string.Empty;

        /// <summary>
        /// 通道所属工步名称。
        /// </summary>
        public string StepName { get; set; } = string.Empty;

        /// <summary>
        /// 测量结果
        /// </summary>
        public MeasurementResult Result { get; set; }

        /// <summary>
        /// 是否启用显示文本。
        /// </summary>
        public string IsEnabledText => IsEnabled ? "是" : "否";

        /// <summary>
        /// 是否校准显示文本。
        /// </summary>
        public string RequiresCalibrationText => RequiresCalibration ? "是" : "否";

        /// <summary>
        /// 校准方式显示文本。
        /// </summary>
        public string CalibrationModeText => CalibrationMode.GetDescription();

        /// <summary>
        /// 是否使用缓存显示文本。
        /// </summary>
        public string UseCacheValueText => UseCacheValue ? "是" : "否";

        /// <summary>
        /// 通道结果显示文本。
        /// </summary>
        public string ResultText => Result.GetDescription();

        /// <summary>
        /// 通道编号显示文本。
        /// 优先显示持久化后的带前缀编号，兼容旧记录时回退为纯数字编号。
        /// </summary>
        public string ChannelNumberDisplay => string.IsNullOrWhiteSpace(ChannelNumberText)
            ? ChannelNumber.ToString(CultureInfo.InvariantCulture)
            : ChannelNumberText;

        /// <summary>
        /// 工步编号显示文本。
        /// 优先使用持久化显示值，兼容旧记录时回退为数字工步号。
        /// </summary>
        public string StepNumberDisplay => string.IsNullOrWhiteSpace(StepDisplayText)
            ? StepNumber.ToString(CultureInfo.InvariantCulture)
            : StepDisplayText;
    }
}
