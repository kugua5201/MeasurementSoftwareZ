using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.ViewModels;
using MultiProtocol.Model;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace MeasurementSoftware.Models
{
    /// <summary>
    /// 数据点模型
    /// </summary>
    public partial class DataPoint : ObservableViewModel, IDataErrorInfo
    {
        /// <summary>
        /// 点位ID
        /// </summary>
        [ObservableProperty]
        private string pointId = string.Empty;

        /// <summary>
        /// 点位名称
        /// </summary>
        [ObservableProperty]
        private string pointName = string.Empty;


        /// <summary>
        /// 数据地址（如：DB1.DBD0、D100、400001等）
        /// </summary>
        [ObservableProperty]
        private string address = string.Empty;

        /// <summary>
        /// 数据类型（Int、Float、Bool等）
        /// </summary>
        [ObservableProperty]
        private FieldType dataType = FieldType.Float;

        /// <summary>
        /// 字节序（ABCD、BADC、CDAB、DCBA）
        /// </summary>
        [ObservableProperty]
        private ByteOrder byteOrder = ByteOrder.DCBA;

        /// <summary>
        /// 字符串长度。
        /// 仅当数据类型为 String 时生效。
        /// </summary>
        private int dataLength = 20;

        public int DataLength
        {
            get => dataLength;
            set => SetProperty(ref dataLength, value);
        }

        /// <summary>
        /// 当前值
        /// </summary>
        [ObservableProperty]
        private object? currentValue;

        /// <summary>
        /// 是否读取成功
        /// </summary>
        [ObservableProperty]
        private bool isSuccess = true;

        /// <summary>
        /// 错误消息
        /// </summary>
        [ObservableProperty]
        private string? errorMessage;

        /// <summary>
        /// 最后更新时间
        /// </summary>
        [ObservableProperty]
        private DateTime lastUpdateTime = DateTime.Now;

        /// <summary>
        /// 是否启用
        /// </summary>
        [ObservableProperty]
        private bool isEnabled = true;

        /// <summary>
        /// 所属设备ID
        /// </summary>
        [ObservableProperty]
        private string deviceId = string.Empty;

        /// <summary>
        /// 验证状态（用于UI显示）
        /// </summary>
        [ObservableProperty]
        private string? validationStatus = "未检查";

        /// <summary>
        /// 验证错误消息
        /// </summary>
        [ObservableProperty]
        private string? validationError = string.Empty;

        /// <summary>
        /// 验证是否通过
        /// </summary>
        [ObservableProperty]
        private bool isValidated = false;

        /// <summary>
        /// 是否由缓存结构自动生成（用于区分手动添加的点位）
        /// </summary>
        [ObservableProperty]
        private bool isCacheGenerated = false;

        /// <summary>
        /// 缓存字段键（格式：CACHE:{字段名}），仅缓存生成的点位有值。
        /// </summary>
        [ObservableProperty]
        private string cacheFieldKey = string.Empty;


        /// <summary>
        /// 是否启用超限预设。
        /// 启用后，当测量值低于下限值或高于上限值时，使用对应的预设值替代。
        /// </summary>
        [ObservableProperty]
        private bool enableLimitPreset;

        /// <summary>
        /// 下限值。
        /// 当测量值小于该值时，认为发生超下限。
        /// </summary>
        [ObservableProperty]
        private double limitLowerValue = -3;

        /// <summary>
        /// 超下限时使用的预设值。
        /// 当测量值小于 <see cref="LimitLowerValue"/> 时，使用该值进行替代。
        /// </summary>
        [ObservableProperty]
        private double presetValueWhenBelowLimit = -5;

        /// <summary>
        /// 上限值。
        /// 当测量值大于该值时，认为发生超上限。
        /// </summary>
        [ObservableProperty]
        private double limitUpperValue = 3;

        /// <summary>
        /// 超上限时使用的预设值。
        /// 当测量值大于 <see cref="LimitUpperValue"/> 时，使用该值进行替代。
        /// </summary>
        [ObservableProperty]
        private double presetValueWhenAboveLimit = 5;


        [JsonIgnore]
        public string Error => string.Empty;

        public string this[string columnName]
        {
            get
            {
                if (IsLimitPresetProperty(columnName))
                {
                    if (!EnableLimitPreset)
                        return string.Empty;

                    switch (columnName)
                    {
                        case nameof(LimitLowerValue):
                            if (HasFiniteLowerAndUpperLimit() && LimitLowerValue >= LimitUpperValue)
                                return "下限值必须小于上限值";
                            break;

                        case nameof(LimitUpperValue):
                            if (HasFiniteLowerAndUpperLimit() && LimitUpperValue <= LimitLowerValue)
                                return "上限值必须大于下限值";
                            break;

                        case nameof(PresetValueWhenBelowLimit):
                            if (!double.IsNegativeInfinity(LimitLowerValue) &&
                                PresetValueWhenBelowLimit >= LimitLowerValue)
                            {
                                return "超下限预设值应小于下限值";
                            }
                            break;

                        case nameof(PresetValueWhenAboveLimit):
                            if (!double.IsPositiveInfinity(LimitUpperValue) &&
                                PresetValueWhenAboveLimit <= LimitUpperValue)
                            {
                                return "超上限预设值应大于上限值";
                            }
                            break;
                    }
                }

                return string.Empty;
            }
        }

        private bool IsLimitPresetProperty(string columnName)
        {
            return columnName == nameof(LimitLowerValue)
                || columnName == nameof(LimitUpperValue)
                || columnName == nameof(PresetValueWhenBelowLimit)
                || columnName == nameof(PresetValueWhenAboveLimit);
        }

        private bool HasFiniteLowerAndUpperLimit()
        {
            return !double.IsNegativeInfinity(LimitLowerValue)
                && !double.IsPositiveInfinity(LimitUpperValue);
        }

        public bool CanUseLimitPreset =>
                    DataType == FieldType.Byte ||
                    DataType == FieldType.Int16 ||
                    DataType == FieldType.UInt16 ||
                    DataType == FieldType.Int32 ||
                    DataType == FieldType.UInt32 ||
                    DataType == FieldType.Int64 ||
                    DataType == FieldType.UInt64 ||
                    DataType == FieldType.Long ||
                    DataType == FieldType.Float ||
                    DataType == FieldType.Double;
        partial void OnDataTypeChanged(FieldType value)
        {
            OnPropertyChanged(nameof(CanUseLimitPreset));

            if (!CanUseLimitPreset)
            {
                EnableLimitPreset = false;
            }
        }
        public string LimitPresetToolTip => CanUseLimitPreset ? "启用超限预设" : "当前数据类型不支持超限预设";
    }
}
