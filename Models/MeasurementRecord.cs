using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.ViewModels;

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
        /// 通道编号
        /// </summary>
        public int ChannelNumber { get; set; }

        /// <summary>
        /// 通道名称
        /// </summary>
        public string ChannelName { get; set; } = string.Empty;

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
        public double MeasuredValue { get; set; }

        /// <summary>
        /// 测量结果
        /// </summary>
        public MeasurementResult Result { get; set; }
    }
}
