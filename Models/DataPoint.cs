using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.ViewModels;

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
        /// 点位描述
        /// </summary>
        [ObservableProperty]
        private string pointDescription = string.Empty;

        /// <summary>
        /// 数据地址（如：DB1.DBD0、D100、400001等）
        /// </summary>
        [ObservableProperty]
        private string address = string.Empty;

        /// <summary>
        /// 数据类型（Int、Float、Bool等）
        /// </summary>
        [ObservableProperty]
        private string dataType = "Float";

        /// <summary>
        /// 字节序（ABCD、BADC、CDAB、DCBA）
        /// </summary>
        [ObservableProperty]
        private string byteOrder = "DCBA";

        /// <summary>
        /// 当前值
        /// </summary>
        [ObservableProperty]
        private object? currentValue;

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
    }
}
