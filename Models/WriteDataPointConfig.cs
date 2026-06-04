using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.ViewModels;
using MultiProtocol.Model;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;

namespace MeasurementSoftware.Models
{
    /// <summary>
    /// 写入点位值交互模式。
    /// </summary>
    public enum WriteValueEditorMode
    {
        /// <summary>
        /// 仅标签显示。
        /// </summary>
        [Description("标签显示")]
        Label,

        /// <summary>
        /// 双击输入并回车提交。
        /// </summary>              
        [Description("输入输出")]
        TextBox,

        /// <summary>
        /// 按钮点击后输入。
        /// </summary>
        [Description("按钮写入")]
        Button
    }

    /// <summary>
    /// 标签模式下的值显示方式。
    /// </summary>
    public enum WriteValueLabelDisplayMode
    {
        /// <summary>
        /// 直接显示原始值。
        /// </summary>
        [Description("原值显示")]
        RawValue,

        /// <summary>
        /// 按规则映射显示。
        /// </summary>
        [Description("规则显示")]
        RuleBased
    }

    /// <summary>
    /// 按钮模式下的交互方式。
    /// </summary>
    public enum WriteValueButtonInteractionMode
    {
        /// <summary>
        /// 点击按钮直接写入单个预设值。
        /// </summary>
        [Description("点击写入")]
        Click,

        /// <summary>
        /// 鼠标按下时写入按下值，鼠标松开时写入松开值。
        /// </summary>
        [Description("按下/松开写入")]
        PressAndRelease
    }

    /// <summary>
    /// 写入点位单条显示规则。
    /// 规则基于采集到的原始值字符串进行匹配，并输出自定义显示文本。
    /// </summary>
    public class WriteValueDisplayRule : ObservableViewModel
    {
        private string sourceValue = string.Empty;
        private string displayText = string.Empty;

        /// <summary>
        /// 原始值。
        /// </summary>
        public string SourceValue
        {
            get => sourceValue;
            set => SetProperty(ref sourceValue, value);
        }

        /// <summary>
        /// 显示文本。
        /// </summary>
        public string DisplayText
        {
            get => displayText;
            set => SetProperty(ref displayText, value);
        }
    }

    /// <summary>
    /// 写入点位配置。
    /// 用于定义写入目标、展示风格和标签模式下的规则映射。
    /// </summary>
    public class WriteDataPointConfig : ObservableViewModel, IDataErrorInfo
    {
        private bool isEnabled = true;
        private int index;
        private string displayName = string.Empty;
        private long plcDeviceId;
        private string dataPointId = string.Empty;
        private FieldType dataType;
        private string unit = string.Empty;
        private string description = string.Empty;
        private WriteValueEditorMode editorMode = WriteValueEditorMode.Label;
        private WriteValueLabelDisplayMode labelDisplayMode = WriteValueLabelDisplayMode.RawValue;
        private string defaultDisplayText = "--";
        private string ruleScriptText = string.Empty;
        private string ruleScriptStatusText = string.Empty;
        private bool isRuleScriptValid = true;
        private string pendingWriteValueText = string.Empty;
        private WriteValueButtonInteractionMode buttonInteractionMode = WriteValueButtonInteractionMode.Click;
        private string buttonWriteValueText = string.Empty;
        private string buttonReleaseWriteValueText = string.Empty;
        private string buttonDisplayText = "按钮1";
        private string editingWriteValueText = string.Empty;
        private bool isValueEditing;
        private string writeStatusText = string.Empty;
        private bool? isWriteStatusSuccess;
        private ObservableCollection<DataPoint> availableDataPoints = [];
        private string currentValueDisplayText = "--";

        /// <summary>
        /// 是否启用。
        /// </summary>
        public bool IsEnabled
        {
            get => isEnabled;
            set => SetProperty(ref isEnabled, value);
        }

        public int Index
        {
            get => index;
            set => SetProperty(ref index, value);
        }

        /// <summary>
        /// 显示名称。
        /// </summary>
        public string DisplayName
        {
            get => displayName;
            set => SetProperty(ref displayName, value);
        }

        /// <summary>
        /// 关联设备 ID。
        /// </summary>
        public long PlcDeviceId
        {
            get => plcDeviceId;
            set => SetProperty(ref plcDeviceId, value);
        }

        /// <summary>
        /// 关联点位 ID。
        /// </summary>
        public string DataPointId
        {
            get => dataPointId;
            set => SetProperty(ref dataPointId, value);
        }

        /// <summary>
        /// 数据类型。
        /// 用于界面输入解析和规则匹配提示。
        /// </summary>
        public FieldType DataType
        {
            get => dataType;
            set => SetProperty(ref dataType, value);
        }

        /// <summary>
        /// 单位。
        /// </summary>
        public string Unit
        {
            get => unit;
            set => SetProperty(ref unit, value);
        }

        /// <summary>
        /// 说明。
        /// </summary>
        public string Description
        {
            get => description;
            set => SetProperty(ref description, value, () => OnPropertyChanged(nameof(DisplayDescription)));
        }

        /// <summary>
        /// 值交互模式。
        /// </summary>
        public WriteValueEditorMode EditorMode
        {
            get => editorMode;
            set => SetProperty(ref editorMode, value, () =>
            {
                if (editorMode != WriteValueEditorMode.Label)
                {
                    LabelDisplayMode = WriteValueLabelDisplayMode.RawValue;
                }

                OnPropertyChanged(nameof(IsLabelMode));
                OnPropertyChanged(nameof(IsTextBoxMode));
                OnPropertyChanged(nameof(IsButtonMode));
                OnPropertyChanged(nameof(UsesRuleDisplay));
                OnPropertyChanged(nameof(CurrentValueDisplayText));
                OnPropertyChanged(nameof(EditorModeDisplayText));
                OnPropertyChanged(nameof(ButtonPrimaryValueLabel));
                OnPropertyChanged(nameof(ButtonPrimaryValuePlaceholder));
                OnPropertyChanged(nameof(ButtonReleaseValuePlaceholder));
                OnPropertyChanged(nameof(ButtonWriteValueText));
                OnPropertyChanged(nameof(ButtonReleaseWriteValueText));
            });
        }

        /// <summary>
        /// 标签显示方式。
        /// </summary>
        public WriteValueLabelDisplayMode LabelDisplayMode
        {
            get => labelDisplayMode;
            set
            {
                var nextValue = EditorMode == WriteValueEditorMode.Label
                    ? value
                    : WriteValueLabelDisplayMode.RawValue;

                SetProperty(ref labelDisplayMode, nextValue, () =>
                {
                    OnPropertyChanged(nameof(UsesRuleDisplay));
                    OnPropertyChanged(nameof(CurrentValueDisplayText));
                });
            }
        }

        /// <summary>
        /// 默认显示文本。
        /// 规则未命中时使用。
        /// </summary>
        public string DefaultDisplayText
        {
            get => defaultDisplayText;
            set => SetProperty(ref defaultDisplayText, value);
        }

        /// <summary>
        /// 标签规则脚本文本。
        /// 支持逐行使用“条件=显示值”格式，条件可为 >、<、>=、<= 或 && 组合范围，default=文本 表示默认显示。
        /// </summary>
        public string RuleScriptText
        {
            get => ruleScriptText;
            set => SetProperty(ref ruleScriptText, value);
        }

        [JsonIgnore]
        public string RuleScriptStatusText
        {
            get => ruleScriptStatusText;
            set => SetProperty(ref ruleScriptStatusText, value);
        }

        [JsonIgnore]
        public bool IsRuleScriptValid
        {
            get => isRuleScriptValid;
            set => SetProperty(ref isRuleScriptValid, value);
        }

        [JsonIgnore]
        public string PendingWriteValueText
        {
            get => pendingWriteValueText;
            set => SetProperty(ref pendingWriteValueText, value);
        }

        public string ButtonWriteValueText
        {
            get => buttonWriteValueText;
            set => SetProperty(ref buttonWriteValueText, value);
        }

        /// <summary>
        /// 按钮交互模式。
        /// </summary>
        public WriteValueButtonInteractionMode ButtonInteractionMode
        {
            get => buttonInteractionMode;
            set => SetProperty(ref buttonInteractionMode, value, () =>
            {
                OnPropertyChanged(nameof(IsButtonClickWriteMode));
                OnPropertyChanged(nameof(IsButtonPressAndReleaseMode));
                OnPropertyChanged(nameof(EditorModeDisplayText));
                OnPropertyChanged(nameof(ButtonPrimaryValueLabel));
                OnPropertyChanged(nameof(ButtonPrimaryValuePlaceholder));
                OnPropertyChanged(nameof(ButtonReleaseValuePlaceholder));
                OnPropertyChanged(nameof(ButtonWriteValueText));
                OnPropertyChanged(nameof(ButtonReleaseWriteValueText));
            });
        }

        /// <summary>
        /// 按钮松开时写入的预设值。
        /// </summary>
        public string ButtonReleaseWriteValueText
        {
            get => buttonReleaseWriteValueText;
            set => SetProperty(ref buttonReleaseWriteValueText, value);
        }

        public string ButtonDisplayText
        {
            get => buttonDisplayText;
            set => SetProperty(ref buttonDisplayText, value);
        }

        [JsonIgnore]
        public string EditingWriteValueText
        {
            get => editingWriteValueText;
            set => SetProperty(ref editingWriteValueText, value);
        }

        [JsonIgnore]
        public bool IsValueEditing
        {
            get => isValueEditing;
            set => SetProperty(ref isValueEditing, value, () =>
            {
                OnPropertyChanged(nameof(IsValueDisplayMode));
                OnPropertyChanged(nameof(HasWriteStatus));
            });
        }

        [JsonIgnore]
        public string WriteStatusText
        {
            get => writeStatusText;
            set => SetProperty(ref writeStatusText, value, () => OnPropertyChanged(nameof(HasWriteStatus)));
        }

        [JsonIgnore]
        public bool? IsWriteStatusSuccess
        {
            get => isWriteStatusSuccess;
            set => SetProperty(ref isWriteStatusSuccess, value);
        }

        /// <summary>
        /// 是否允许外部页面直接显示原始值。
        /// </summary>
        [JsonIgnore]
        public bool IsLabelMode => EditorMode == WriteValueEditorMode.Label;

        /// <summary>
        /// 是否为双击输入模式。
        /// </summary>
        [JsonIgnore]
        public bool IsTextBoxMode => EditorMode == WriteValueEditorMode.TextBox;

        /// <summary>
        /// 是否为按钮输入模式。
        /// </summary>
        [JsonIgnore]
        public bool IsButtonMode => EditorMode == WriteValueEditorMode.Button;

        [JsonIgnore]
        public bool IsButtonClickWriteMode => IsButtonMode && ButtonInteractionMode == WriteValueButtonInteractionMode.Click;

        [JsonIgnore]
        public bool IsButtonPressAndReleaseMode => IsButtonMode && ButtonInteractionMode == WriteValueButtonInteractionMode.PressAndRelease;

        [JsonIgnore]
        public string ButtonPrimaryValueLabel => IsButtonPressAndReleaseMode ? "按下值：" : "点击值：";

        [JsonIgnore]
        public string ButtonPrimaryValuePlaceholder => IsButtonPressAndReleaseMode ? "请输入按钮按下时写入的值" : "请输入按钮点击时写入的值";

        [JsonIgnore]
        public string ButtonReleaseValuePlaceholder => "请输入按钮松开时写入的值";

        [JsonIgnore]
        public string EditorModeDisplayText
        {
            get
            {
                if (!IsButtonMode)
                {
                    var description = typeof(WriteValueEditorMode)
                        .GetField(EditorMode.ToString())?
                        .GetCustomAttributes(typeof(DescriptionAttribute), false)
                        .OfType<DescriptionAttribute>()
                        .FirstOrDefault()?.Description;

                    return string.IsNullOrWhiteSpace(description) ? EditorMode.ToString() : description;
                }

                return IsButtonPressAndReleaseMode ? "按钮输入（按下/松开）" : "按钮输入（点击）";
            }
        }

        [JsonIgnore]
        public bool CanEditValue => EditorMode != WriteValueEditorMode.Label;

        [JsonIgnore]
        public bool IsValueDisplayMode => !IsValueEditing;

        [JsonIgnore]
        public bool HasWriteStatus => !string.IsNullOrWhiteSpace(WriteStatusText);

        [JsonIgnore]
        public string DisplayDescription => HasWriteStatus ? WriteStatusText : Description;

        [JsonIgnore]
        public bool IsDisplayDescriptionStatus => HasWriteStatus;

        /// <summary>
        /// 是否启用规则显示。
        /// </summary>
        [JsonIgnore]
        public bool UsesRuleDisplay => EditorMode == WriteValueEditorMode.Label && LabelDisplayMode == WriteValueLabelDisplayMode.RuleBased;

        /// <summary>
        /// 当前用于界面展示的值文本。
        /// </summary>
        [JsonIgnore]
        public string CurrentValueDisplayText
        {
            get => currentValueDisplayText;
            set => SetProperty(ref currentValueDisplayText, value);
        }

        /// <summary>
        /// 当前设备下可选的点位列表。
        /// </summary>
        public ObservableCollection<DataPoint> AvailableDataPoints
        {
            get => availableDataPoints;
            set => SetProperty(ref availableDataPoints, value ?? [], () =>
            {
                OnPropertyChanged(nameof(SelectedPointName));
                OnPropertyChanged(nameof(SelectedDeviceName));
            });
        }

        /// <summary>
        /// 标签模式下的显示规则集合。
        /// </summary>
        public ObservableCollection<WriteValueDisplayRule> DisplayRules { get; set; } = [];

        private PlcDevice? runtimeDevice;
        private DataPoint? runtimeDataPoint;

        /// <summary>
        /// 当前绑定的运行时设备实例。
        /// </summary>
        [JsonIgnore]
        public PlcDevice? RuntimeDevice
        {
            get => runtimeDevice;
            set => SetProperty(ref runtimeDevice, value, () =>
            {
                OnPropertyChanged(nameof(SelectedDeviceName));
                OnPropertyChanged(nameof(RuntimeDevice));
            });
        }

        /// <summary>
        /// 当前绑定的运行时点位实例。
        /// </summary>
        [JsonIgnore]
        public DataPoint? RuntimeDataPoint
        {
            get => runtimeDataPoint;
            set => SetProperty(ref runtimeDataPoint, value, () =>
            {
                OnPropertyChanged(nameof(SelectedPointName));
                OnPropertyChanged(nameof(RuntimeDataPoint));
            });
        }

        /// <summary>
        /// 当前绑定设备显示名称。
        /// </summary>
        [JsonIgnore]
        public string SelectedDeviceName => RuntimeDevice?.DeviceName ?? string.Empty;

        /// <summary>
        /// 当前绑定点位显示名称。
        /// </summary>
        [JsonIgnore]
        public string SelectedPointName => RuntimeDataPoint?.PointName ?? string.Empty;

        public bool BeginValueEdit()
        {
            if (!CanEditValue)
            {
                return false;
            }

            EditingWriteValueText = string.IsNullOrWhiteSpace(PendingWriteValueText)
                ? RuntimeDataPoint?.CurrentValue?.ToString() ?? string.Empty
                : PendingWriteValueText;
            IsValueEditing = true;
            return true;
        }

        public void CancelValueEdit()
        {
            EditingWriteValueText = string.Empty;
            IsValueEditing = false;
        }

        public void CompleteValueEdit(string? committedValueText)
        {
            PendingWriteValueText = committedValueText ?? string.Empty;
            EditingWriteValueText = PendingWriteValueText;
            IsValueEditing = false;
        }

        public void SyncPendingWriteValueFromRuntime()
        {
            if (IsValueEditing)
            {
                return;
            }

            PendingWriteValueText = RuntimeDataPoint?.CurrentValue?.ToString() ?? string.Empty;
        }

        public void SetWriteStatus(string text, bool? success)
        {
            WriteStatusText = text;
            IsWriteStatusSuccess = success;
            OnPropertyChanged(nameof(DisplayDescription));
            OnPropertyChanged(nameof(IsDisplayDescriptionStatus));
        }

        public void ClearWriteStatus()
        {
            WriteStatusText = string.Empty;
            IsWriteStatusSuccess = null;
            OnPropertyChanged(nameof(DisplayDescription));
            OnPropertyChanged(nameof(IsDisplayDescriptionStatus));
        }

        [JsonIgnore]
        public string Error => string.Empty;

        public string this[string columnName]
        {
            get
            {
                if (columnName == nameof(EditingWriteValueText))
                {
                    if (!IsValueEditing)
                    {
                        return string.Empty;
                    }

                    if (TryConvertWriteValue(EditingWriteValueText, DataType, out _, out var editErrorMessage))
                    {
                        return string.Empty;
                    }

                    return $"验证失败：{editErrorMessage}";
                }

                if (columnName == nameof(ButtonWriteValueText))
                {
                    if (!IsButtonMode)
                    {
                        return string.Empty;
                    }

                    if (string.IsNullOrWhiteSpace(ButtonWriteValueText))
                    {
                        return $"验证失败：{(IsButtonPressAndReleaseMode ? "请输入按钮按下值" : "请输入按钮点击值")}";
                    }

                    if (TryConvertWriteValue(ButtonWriteValueText, DataType, out _, out var buttonErrorMessage))
                    {
                        return string.Empty;
                    }

                    return $"验证失败：{buttonErrorMessage}";
                }

                if (columnName == nameof(ButtonReleaseWriteValueText))
                {
                    if (!IsButtonPressAndReleaseMode)
                    {
                        return string.Empty;
                    }

                    if (string.IsNullOrWhiteSpace(ButtonReleaseWriteValueText))
                    {
                        return "验证失败：请输入按钮松开值";
                    }

                    if (TryConvertWriteValue(ButtonReleaseWriteValueText, DataType, out _, out var releaseErrorMessage))
                    {
                        return string.Empty;
                    }

                    return $"验证失败：{releaseErrorMessage}";
                }

                return string.Empty;
            }
        }

        public static bool TryConvertWriteValue(string rawValue, FieldType dataType, out object? value, out string errorMessage)
        {
            errorMessage = string.Empty;
            value = null;

            try
            {
                switch (dataType)
                {
                    case FieldType.Bool:
                        if (bool.TryParse(rawValue, out var boolValue))
                        {
                            value = boolValue;
                            return true;
                        }

                        if (rawValue == "1")
                        {
                            value = true;
                            return true;
                        }

                        if (rawValue == "0")
                        {
                            value = false;
                            return true;
                        }

                        errorMessage = "Bool 类型请输入 true/false 或 1/0";
                        return false;
                    case FieldType.Int16:
                        value = short.Parse(rawValue, CultureInfo.InvariantCulture);
                        return true;
                    case FieldType.UInt16:
                        value = ushort.Parse(rawValue, CultureInfo.InvariantCulture);
                        return true;
                    case FieldType.Int32:
                        value = int.Parse(rawValue, CultureInfo.InvariantCulture);
                        return true;
                    case FieldType.UInt32:
                        value = uint.Parse(rawValue, CultureInfo.InvariantCulture);
                        return true;
                    case FieldType.Int64:
                    case FieldType.Long:
                        value = long.Parse(rawValue, CultureInfo.InvariantCulture);
                        return true;
                    case FieldType.UInt64:
                        value = ulong.Parse(rawValue, CultureInfo.InvariantCulture);
                        return true;
                    case FieldType.Float:
                        value = float.Parse(rawValue, CultureInfo.InvariantCulture);
                        return true;
                    case FieldType.Double:
                        value = double.Parse(rawValue, CultureInfo.InvariantCulture);
                        return true;
                    case FieldType.String:
                        value = rawValue;
                        return true;
                    default:
                        value = rawValue;
                        return true;
                }
            }
            catch
            {
                errorMessage = $"输入值无法转换为 {dataType}";
                return false;
            }
        }

        public void SetRuleScriptValidationState(bool isValid, string statusText)
        {
            IsRuleScriptValid = isValid;
            RuleScriptStatusText = statusText;
        }
    }
}