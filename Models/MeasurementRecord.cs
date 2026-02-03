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
