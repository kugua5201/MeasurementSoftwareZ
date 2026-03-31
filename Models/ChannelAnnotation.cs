using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.ViewModels;
using System.Text.Json.Serialization;

namespace MeasurementSoftware.Models
{
    /// <summary>
    /// 通道标注点（在产品图片上标注测量位置）
    /// </summary>
    public partial class ChannelAnnotation : ObservableViewModel
    {
        /// <summary>
        /// 标注点X坐标（相对于图片宽度的比例 0~1）
        /// </summary>
        [ObservableProperty]
        private double x;

        /// <summary>
        /// 标注点Y坐标（相对于图片高度的比例 0~1）
        /// </summary>
        [ObservableProperty]
        private double y;

        /// <summary>
        /// 关联的通道编号
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ResolvedDisplayText))]
        private int channelNumber;

        /// <summary>
        /// 关联的工步编号
        /// </summary>
        [ObservableProperty]
        private int stepNumber = 1;

        /// <summary>
        /// 标注显示文本
        /// </summary>
        [ObservableProperty]
        private string label = string.Empty;

        /// <summary>
        /// 通道名称（用于显示）
        /// </summary>
        [ObservableProperty]
        private string channelName = string.Empty;

        /// <summary>
        /// 测量结果（用于标注颜色：NotMeasured=蓝, Pass=绿, Fail=红）
        /// </summary>
        [ObservableProperty]
        private MeasurementResult result = MeasurementResult.NotMeasured;

        /// <summary>
        /// 标注显示状态。
        /// 复用测量结果枚举，支持等待、采集中、OK、NG。
        /// </summary>
        [ObservableProperty]
        private MeasurementResult displayState = MeasurementResult.Waiting;

        /// <summary>
        /// 自定义显示文本（留空则显示通道编号）
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ResolvedDisplayText))]
        private string displayText = string.Empty;

        /// <summary>
        /// 是否在当前工步中激活显示（运行时状态，不序列化）
        /// </summary>
        [ObservableProperty]
        [property: JsonIgnore]
        private bool isActiveInCurrentStep;

        /// <summary>
        /// 解析后的显示文本（优先使用自定义文本，否则使用通道编号）
        /// </summary>
        [JsonIgnore]
        public string ResolvedDisplayText => !string.IsNullOrEmpty(DisplayText) ? DisplayText : ChannelNumber.ToString();
    }
}
