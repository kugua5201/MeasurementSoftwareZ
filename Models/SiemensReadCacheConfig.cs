using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.ViewModels;

namespace MeasurementSoftware.Models
{
    /// <summary>
    /// 西门子读取缓存配置
    /// </summary>
    public partial class SiemensReadCacheConfig : ObservableViewModel
    {
        /// <summary>
        /// 是否启用读取缓存
        /// </summary>
        [ObservableProperty]
        private bool isEnabled;

        /// <summary>
        /// 缓存1配置
        /// </summary>
        [ObservableProperty]
        private SiemensReadCacheItemConfig cache1 = new()
        {
            DbBlock = "DBX",
            Length = 256,
            IsReadable = true
        };

        /// <summary>
        /// 缓存2配置
        /// </summary>
        [ObservableProperty]
        private SiemensReadCacheItemConfig cache2 = new()
        {
            DbBlock = "DBD",
            Length = 256,
            IsReadable = true
        };
    }

    /// <summary>
    /// 单个西门子缓存项配置
    /// </summary>
    public partial class SiemensReadCacheItemConfig : ObservableViewModel
    {
        /// <summary>
        /// DB块标识，例如：DBX、DBD
        /// </summary>
        [ObservableProperty]
        private string dbBlock = "DBX";

        /// <summary>
        /// 读取长度
        /// </summary>
        [ObservableProperty]
        private ushort length = 256;

        /// <summary>
        /// 是否可读
        /// </summary>
        [ObservableProperty]
        private bool isReadable = true;
    }
}
