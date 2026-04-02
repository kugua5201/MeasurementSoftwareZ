using MeasurementSoftware.ViewModels;
using System.Collections.ObjectModel;
using System.Linq;
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
        private bool enableStepOperationBinding;
        private int totalSteps = 10;
        private int acquisitionDelayMs = 500;
        private double annotationSize = 28;
        private AnnotationShape annotationShape = AnnotationShape.圆形;
        private string okColor = "#4CAF50";
        private string ngColor = "#F44336";
        private string defaultColor = "#2196F3";
        private string acquiringColor = "#FF9800";
        private string channelDisplayPrefix = "M";
        private AnnotationDisplayFormat annotationDisplayFormat = AnnotationDisplayFormat.通道编号;
        private double annotationFontSize = 10;
        private string annotationTextColor = "#FFFFFF";
        private AcquisitionStorageConfig acquisitionStorage = new();
        private ObservableCollection<StepOperationBindingConfig> stepOperationBindings = CreateDefaultStepOperationBindings();

        public RecipeOtherSettingsConfig()
        {
            EnsureStepOperationBindings();
        }

        /// <summary>
        /// 是否启用分步测量模式。
        /// </summary>
        public bool EnableStepMode
        {
            get => enableStepMode;
            set => SetProperty(ref enableStepMode, value);
        }

        /// <summary>
        /// 是否启用点位绑定监听。
        /// 关闭时仅允许手动点击开始/停止/上下工步按钮，不监听点位触发。
        /// </summary>
        public bool EnableStepOperationBinding
        {
            get => enableStepOperationBinding;
            set => SetProperty(ref enableStepOperationBinding, value);
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
            set => SetProperty(ref okColor, NormalizeColorString(value, "#4CAF50"), () => OnPropertyChanged(nameof(OkBrush)));
        }

        /// <summary>
        /// NG 颜色。
        /// </summary>
        public string NgColor
        {
            get => ngColor;
            set => SetProperty(ref ngColor, NormalizeColorString(value, "#F44336"), () => OnPropertyChanged(nameof(NgBrush)));
        }

        /// <summary>
        /// 默认颜色。
        /// </summary>
        public string DefaultColor
        {
            get => defaultColor;
            set => SetProperty(ref defaultColor, NormalizeColorString(value, "#2196F3"), () => OnPropertyChanged(nameof(DefaultBrush)));
        }

        /// <summary>
        /// 采集中颜色。
        /// </summary>
        public string AcquiringColor
        {
            get => acquiringColor;
            set => SetProperty(ref acquiringColor, NormalizeColorString(value, "#FF9800"), () => OnPropertyChanged(nameof(AcquiringBrush)));
        }

        /// <summary>
        /// 通道编号显示前缀。
        /// </summary>
        public string ChannelDisplayPrefix
        {
            get => channelDisplayPrefix;
            set => SetProperty(ref channelDisplayPrefix, string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim());
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
            set => SetProperty(ref annotationTextColor, NormalizeColorString(value, "#FFFFFF"), () => OnPropertyChanged(nameof(AnnotationTextBrush)));
        }

        /// <summary>
        /// 采集结果存储规则配置。
        /// </summary>
        public AcquisitionStorageConfig AcquisitionStorage
        {
            get => acquisitionStorage;
            set => SetProperty(ref acquisitionStorage, value ?? new AcquisitionStorageConfig());
        }

        /// <summary>
        /// 工步操作绑定配置。
        /// </summary>
        public ObservableCollection<StepOperationBindingConfig> StepOperationBindings
        {
            get => stepOperationBindings;
            set => SetProperty(ref stepOperationBindings, value ?? [], EnsureStepOperationBindings);
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
                OkColor = ToHexColorString(value?.Color, "#4CAF50");
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
                NgColor = ToHexColorString(value?.Color, "#F44336");
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
                DefaultColor = ToHexColorString(value?.Color, "#2196F3");
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 采集中颜色画刷。
        /// 仅用于界面绑定，不参与序列化。
        /// </summary>
        [JsonIgnore]
        public SolidColorBrush? AcquiringBrush
        {
            get => TryParseBrush(AcquiringColor);
            set
            {
                AcquiringColor = ToHexColorString(value?.Color, "#FF9800");
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
                AnnotationTextColor = ToHexColorString(value?.Color, "#FFFFFF");
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// 工步的点位绑定配置反序列化后需要根据当前设备列表进行一次绑定关系的重新建立，以确保配方配置与当前系统状态的一致性。
        /// </summary>
        /// <param name="devices">当前系统中的设备列表。</param>
        public void HydrateStepOperationBindings(IEnumerable<PlcDevice> devices)
        {
            EnsureStepOperationBindings();

            var deviceList = devices.ToList();

            foreach (var binding in StepOperationBindings)
            {
                var device = binding.RuntimeDevice != null && deviceList.Contains(binding.RuntimeDevice)
                    ? binding.RuntimeDevice
                    : deviceList.FirstOrDefault(d => d.DeviceId == binding.PlcDeviceId);

                binding.HydrateRuntimeBindings(device);
            }
        }
        /// <summary>
        /// 用于确保工步操作绑定配置的完整性和正确性。
        /// </summary>
        private void EnsureStepOperationBindings()
        {
            stepOperationBindings ??= [];

            var normalizedBindings = StepOperationBindings
                .GroupBy(binding => binding.OperationType)
                .Select(group => group.First())
                .ToDictionary(binding => binding.OperationType);

            var orderedBindings = new List<StepOperationBindingConfig>();
            foreach (var operationType in Enum.GetValues<StepOperationType>())
            {
                if (!normalizedBindings.TryGetValue(operationType, out var binding))
                {
                    binding = new StepOperationBindingConfig
                    {
                        OperationType = operationType,
                        TriggerMode = StepOperationTriggerMode.RisingEdge,
                        TriggerValue = "true"
                    };
                }

                orderedBindings.Add(binding);
            }

            var staleBindings = stepOperationBindings
                .Where(binding => !orderedBindings.Contains(binding))
                .ToList();

            foreach (var binding in staleBindings)
            {
                stepOperationBindings.Remove(binding);
            }

            for (int index = 0; index < orderedBindings.Count; index++)
            {
                var binding = orderedBindings[index];
                var currentIndex = stepOperationBindings.IndexOf(binding);

                if (currentIndex < 0)
                {
                    stepOperationBindings.Insert(index, binding);
                    continue;
                }

                if (currentIndex != index)
                {
                    stepOperationBindings.Move(currentIndex, index);
                }
            }
        }

        private static ObservableCollection<StepOperationBindingConfig> CreateDefaultStepOperationBindings()
        {
            return [
                new StepOperationBindingConfig { OperationType = StepOperationType.StartAcquisition, TriggerMode = StepOperationTriggerMode.RisingEdge, TriggerValue = "true" },
                new StepOperationBindingConfig { OperationType = StepOperationType.StopAcquisition, TriggerMode = StepOperationTriggerMode.RisingEdge, TriggerValue = "true" },
                new StepOperationBindingConfig { OperationType = StepOperationType.PreviousStep, TriggerMode = StepOperationTriggerMode.RisingEdge, TriggerValue = "true" },
                new StepOperationBindingConfig { OperationType = StepOperationType.NextStep, TriggerMode = StepOperationTriggerMode.RisingEdge, TriggerValue = "true" },
                new StepOperationBindingConfig { OperationType = StepOperationType.TerminateMeasurement, TriggerMode = StepOperationTriggerMode.RisingEdge, TriggerValue = "true" }
            ];
        }

        private static string NormalizeColorString(string? colorStr, string fallback)
        {
            if (string.IsNullOrWhiteSpace(colorStr))
            {
                return fallback;
            }

            try
            {
                var color = (Color)ColorConverter.ConvertFromString(colorStr);
                return ToHexColorString(color, fallback);
            }
            catch
            {
                return fallback;
            }
        }

        private static string ToHexColorString(Color? color, string fallback)
        {
            if (color is not Color actualColor)
            {
                return fallback;
            }

            return $"#{actualColor.R:X2}{actualColor.G:X2}{actualColor.B:X2}";
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
