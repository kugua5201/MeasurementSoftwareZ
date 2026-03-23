using MeasurementSoftware.ViewModels;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace MeasurementSoftware.Models
{
    /// <summary>
    /// 配方基本信息。
    /// 集中管理配方名称、描述、图片路径及时间元数据，便于后续统一扩展和维护。
    /// </summary>
    public class RecipeBasicInfoConfig : ObservableViewModel
    {
        private string recipeId = string.Empty;
        private string recipeName = string.Empty;
        private string description = string.Empty;
        private string productImagePath = string.Empty;
        private DateTime createTime = DateTime.Now;
        private DateTime modifyTime = DateTime.Now;
        private bool isDefault;

        /// <summary>
        /// 配方ID。
        /// </summary>
        public string RecipeId
        {
            get => recipeId;
            set => SetProperty(ref recipeId, value);
        }

        /// <summary>
        /// 配方名称。
        /// </summary>
        public string RecipeName
        {
            get => recipeName;
            set => SetProperty(ref recipeName, value);
        }

        /// <summary>
        /// 配方描述。
        /// </summary>
        public string Description
        {
            get => description;
            set => SetProperty(ref description, value);
        }

        /// <summary>
        /// 产品图片路径。
        /// </summary>
        public string ProductImagePath
        {
            get => productImagePath;
            set => SetProperty(ref productImagePath, value);
        }

        /// <summary>
        /// 创建时间。
        /// </summary>
        public DateTime CreateTime
        {
            get => createTime;
            set => SetProperty(ref createTime, value);
        }

        /// <summary>
        /// 修改时间。
        /// </summary>
        public DateTime ModifyTime
        {
            get => modifyTime;
            set => SetProperty(ref modifyTime, value);
        }

        /// <summary>
        /// 是否默认配方。
        /// </summary>
        public bool IsDefault
        {
            get => isDefault;
            set => SetProperty(ref isDefault, value);
        }
    }

    /// <summary>
    /// 配方其他设置。
    /// 集中管理采集、工步与标注显示等页面级配置，避免散落在配方根模型中。
    /// </summary>
    public class RecipeOtherSettingsConfig : ObservableViewModel
    {
        private bool enableStepMode;
        private int totalSteps = 10;
        private int acquisitionDelayMs = 500;
        private double annotationSize = 28;
        private AnnotationShape annotationShape = AnnotationShape.圆形;
        private string okColor = "#4CAF50";
        private string ngColor = "#F44336";
        private string defaultColor = "#2196F3";
        private AnnotationDisplayFormat annotationDisplayFormat = AnnotationDisplayFormat.通道编号;
        private double annotationFontSize = 10;
        private string annotationTextColor = "#FFFFFF";

        /// <summary>
        /// 是否启用分步测量模式。
        /// </summary>
        public bool EnableStepMode
        {
            get => enableStepMode;
            set => SetProperty(ref enableStepMode, value);
        }

        /// <summary>
        /// 总工步数。
        /// </summary>
        public int TotalSteps
        {
            get => totalSteps;
            set => SetProperty(ref totalSteps, value);
        }

        /// <summary>
        /// 采集轮询延迟（毫秒）。
        /// </summary>
        public int AcquisitionDelayMs
        {
            get => acquisitionDelayMs;
            set => SetProperty(ref acquisitionDelayMs, value);
        }

        /// <summary>
        /// 标注大小（像素）。
        /// </summary>
        public double AnnotationSize
        {
            get => annotationSize;
            set => SetProperty(ref annotationSize, value);
        }

        /// <summary>
        /// 标注形状。
        /// </summary>
        public AnnotationShape AnnotationShape
        {
            get => annotationShape;
            set => SetProperty(ref annotationShape, value);
        }

        /// <summary>
        /// OK 颜色。
        /// </summary>
        public string OkColor
        {
            get => okColor;
            set => SetProperty(ref okColor, value, () => OnPropertyChanged(nameof(OkBrush)));
        }

        /// <summary>
        /// NG 颜色。
        /// </summary>
        public string NgColor
        {
            get => ngColor;
            set => SetProperty(ref ngColor, value, () => OnPropertyChanged(nameof(NgBrush)));
        }

        /// <summary>
        /// 默认颜色。
        /// </summary>
        public string DefaultColor
        {
            get => defaultColor;
            set => SetProperty(ref defaultColor, value, () => OnPropertyChanged(nameof(DefaultBrush)));
        }

        /// <summary>
        /// 标注显示内容格式。
        /// </summary>
        public AnnotationDisplayFormat AnnotationDisplayFormat
        {
            get => annotationDisplayFormat;
            set => SetProperty(ref annotationDisplayFormat, value);
        }

        /// <summary>
        /// 标注字体大小。
        /// </summary>
        public double AnnotationFontSize
        {
            get => annotationFontSize;
            set => SetProperty(ref annotationFontSize, value);
        }

        /// <summary>
        /// 标注文字颜色。
        /// </summary>
        public string AnnotationTextColor
        {
            get => annotationTextColor;
            set => SetProperty(ref annotationTextColor, value, () => OnPropertyChanged(nameof(AnnotationTextBrush)));
        }

        /// <summary>
        /// OK 颜色画刷。
        /// 仅用于界面绑定，不参与序列化。
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
        /// NG 颜色画刷。
        /// 仅用于界面绑定，不参与序列化。
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

        /// <summary>
        /// 默认颜色画刷。
        /// 仅用于界面绑定，不参与序列化。
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

        /// <summary>
        /// 标注文字颜色画刷。
        /// 仅用于界面绑定，不参与序列化。
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

        private static SolidColorBrush? TryParseBrush(string? colorStr)
        {
            if (string.IsNullOrEmpty(colorStr))
            {
                return null;
            }

            try
            {
                var color = (Color)ColorConverter.ConvertFromString(colorStr);
                return new SolidColorBrush(color);
            }
            catch
            {
                return null;
            }
        }
    }
}
