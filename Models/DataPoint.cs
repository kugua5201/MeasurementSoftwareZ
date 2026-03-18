using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.ViewModels;
using MultiProtocol.Model;

namespace MeasurementSoftware.Models
{
    /// <summary>
    /// 数据点模型
    /// </summary>
    public partial class DataPoint : ObservableViewModel
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
        private string validationStatus = "未检查";

        /// <summary>
        /// 验证错误消息
        /// </summary>
        [ObservableProperty]
        private string validationError = string.Empty;

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
        /// 缓存字段键（格式：CACHE:G{group}:{fieldName}），仅缓存生成的点位有值
        /// </summary>
        [ObservableProperty]
        private string cacheFieldKey = string.Empty;

    }
}
