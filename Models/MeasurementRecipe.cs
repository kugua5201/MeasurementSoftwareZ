using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.ViewModels;
using System.Collections.ObjectModel;

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
        private int totalSteps = 1;

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
