using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.ViewModels;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace MeasurementSoftware.Models
{
    /// <summary>
    /// 测量配方模型
    /// 包含通道、设备、二维码等所有配置，一个配方文件 = 完整的项目配置
    /// </summary>
    public partial class MeasurementRecipe : ObservableViewModel
    {
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
        /// 配方描述
        /// </summary>
        [ObservableProperty]
        private string description = string.Empty;

        /// <summary>
        /// 产品图片路径（跟随配方保存）
        /// </summary>
        [ObservableProperty]
        private string productImagePath = string.Empty;

        /// <summary>
        /// 测量通道集合
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<MeasurementChannel> channels = new ObservableCollection<MeasurementChannel>();

        /// <summary>
        /// 创建时间
        /// </summary>
        [ObservableProperty]
        private DateTime createTime = DateTime.Now;

        /// <summary>
        /// 修改时间
        /// </summary>
        [ObservableProperty]
        private DateTime modifyTime = DateTime.Now;

        /// <summary>
        /// 是否为默认配方
        /// </summary>
        [ObservableProperty]
        private bool isDefault;

        /// <summary>
        /// 工步测量模式：true-分步测量，false-同时测量
        /// </summary>
        [ObservableProperty]
        private bool enableStepMode;

        /// <summary>
        /// 总工步数
        /// </summary>
        [ObservableProperty]
        private int totalSteps = 10;

        /// <summary>
        /// 采集轮询延迟(ms)
        /// </summary>
        [ObservableProperty]
        private int acquisitionDelayMs = 500;

        /// <summary>
        /// 二维码绑定配置
        /// </summary>
        [ObservableProperty]
        private BarcodeBindingConfig barcodeConfig = new();

        /// <summary>
        /// MES上传配置
        /// </summary>
        [ObservableProperty]
        private MesUploadConfig mesConfig = new();

        /// <summary>
        /// PLC数据传输配置
        /// </summary>
        [ObservableProperty]
        private PlcDataTransferConfig plcTransferConfig = new();

        /// <summary>
        /// PLC设备列表（跟随配方保存，切换配方时设备配置一起切换）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<PlcDevice> devices = new();

        /// <summary>
        /// 标注点大小（像素）
        /// </summary>
        [ObservableProperty]
        private double annotationSize = 28;

        /// <summary>
        /// 标注点形状
        /// </summary>
        [ObservableProperty]
        private AnnotationShape annotationShape = AnnotationShape.圆形;

        /// <summary>
        /// OK（合格）颜色
        /// </summary>
        [ObservableProperty]
        private string okColor = "#4CAF50";

        /// <summary>
        /// NG（不合格）颜色
        /// </summary>
        [ObservableProperty]
        private string ngColor = "#F44336";

        /// <summary>
        /// 默认颜色（未测量状态）
        /// </summary>
        [ObservableProperty]
        private string defaultColor = "#2196F3";

        /// <summary>
        /// 标注显示内容格式
        /// </summary>
        [ObservableProperty]
        private AnnotationDisplayFormat annotationDisplayFormat = AnnotationDisplayFormat.通道编号;

        /// <summary>
        /// 标注字体大小
        /// </summary>
        [ObservableProperty]
        private double annotationFontSize = 10;

        /// <summary>
        /// 标注文字颜色
        /// </summary>
        [ObservableProperty]
        private string annotationTextColor = "#FFFFFF";

        /// <summary>
        /// OK颜色画刷（用于ColorPicker绑定）
        /// </summary>
        [JsonIgnore]
        public SolidColorBrush? OkBrush
        {
            get => TryParseBrush(OkColor);
            set
            {
                OkColor = value?.Color.ToString() ?? "#4CAF50";
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// NG颜色画刷（用于ColorPicker绑定）
        /// </summary>
        [JsonIgnore]
        public SolidColorBrush? NgBrush
        {
            get => TryParseBrush(NgColor);
            set
            {
                NgColor = value?.Color.ToString() ?? "#F44336";
                OnPropertyChanged();
            }
        }

        partial void OnOkColorChanged(string value) => OnPropertyChanged(nameof(OkBrush));
        partial void OnNgColorChanged(string value) => OnPropertyChanged(nameof(NgBrush));
        partial void OnDefaultColorChanged(string value) => OnPropertyChanged(nameof(DefaultBrush));
        partial void OnAnnotationTextColorChanged(string value) => OnPropertyChanged(nameof(AnnotationTextBrush));

        /// <summary>
        /// 标注文字颜色画刷（用于绑定）
        /// </summary>
        [JsonIgnore]
        public SolidColorBrush? AnnotationTextBrush
        {
            get => TryParseBrush(AnnotationTextColor);
            set
            {
                AnnotationTextColor = value?.Color.ToString() ?? "#FFFFFF";
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 默认颜色画刷（用于ColorPicker绑定）
        /// </summary>
        [JsonIgnore]
        public SolidColorBrush? DefaultBrush
        {
            get => TryParseBrush(DefaultColor);
            set
            {
                DefaultColor = value?.Color.ToString() ?? "#2196F3";
                OnPropertyChanged();
            }
        }

        private static SolidColorBrush? TryParseBrush(string? colorStr)
        {
            if (string.IsNullOrEmpty(colorStr)) return null;
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(colorStr);
                return new SolidColorBrush(color);
            }
            catch { return null; }
        }

        /// <summary>
        /// 二维码扫码配置（跟随配方保存）
        /// </summary>
        [ObservableProperty]
        private QrCodeConfig qrCodeConfig = new();

        /// <summary>
        /// 获取所有启用的通道
        /// </summary>
        public List<MeasurementChannel> GetEnabledChannels()
        {
            return Channels.Where(c => c.IsEnabled).ToList();
        }

        /// <summary>
        /// 获取指定工步的通道
        /// </summary>
        public List<MeasurementChannel> GetChannelsByStep(int stepNumber)
        {
            return Channels.Where(c => c.IsEnabled && c.StepNumber == stepNumber).ToList();
        }
    }
}
