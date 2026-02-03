using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.ViewModels;
using System.Collections.ObjectModel;

namespace MeasurementSoftware.Models
{
    /// <summary>
    /// 测量配方模型
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
        /// 获取所有启用的通道
        /// </summary>
        public List<MeasurementChannel> GetEnabledChannels()
        {
            return Channels.Where(c => c.IsEnabled).ToList();
        }
    }
}
