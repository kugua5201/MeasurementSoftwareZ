using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MeasurementSoftware.Models
{
    /// <summary>
    /// 测量配方模型
    /// 包含通道、设备、二维码等所有配置，一个配方文件 = 完整的项目配置
    /// </summary>
    public partial class MeasurementRecipe : ObservableViewModel
    {
        private RecipeBasicInfoConfig basicInfo = new();
        private RecipeOtherSettingsConfig otherSettings = new();

        //public MeasurementRecipe()
        //{
        //    SubscribeBasicInfo(basicInfo);
        //    SubscribeOtherSettings(otherSettings);
        //}

        /// <summary>
        /// 配方基本信息。
        /// </summary>
        public RecipeBasicInfoConfig BasicInfo
        {
            get => basicInfo;

            set => SetProperty(ref basicInfo, value);
            //set
            //{
            //    if (ReferenceEquals(basicInfo, value))
            //    {
            //        return;
            //    }

            //    //if (basicInfo != null)
            //    //{
            //    //    basicInfo.PropertyChanged -= BasicInfo_PropertyChanged;
            //    //}

            //    basicInfo = value ?? new RecipeBasicInfoConfig();
            //    //SubscribeBasicInfo(basicInfo);
            //    OnPropertyChanged();
            //}
        }

        /// <summary>
        /// 配方其他设置。
        /// </summary>
        public RecipeOtherSettingsConfig OtherSettings
        {
            get => otherSettings;
            set => SetProperty(ref otherSettings, value);
            //{
            //    if (ReferenceEquals(otherSettings, value))
            //    {
            //        return;
            //    }

            //    //if (otherSettings != null)
            //    //{
            //    //    otherSettings.PropertyChanged -= OtherSettings_PropertyChanged;
            //    //}

            //    otherSettings = value ?? new RecipeOtherSettingsConfig();
            //    //SubscribeOtherSettings(otherSettings);
            //    OnPropertyChanged();
            //}
        }

        /// <summary>
        /// 测量通道集合
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<MeasurementChannel> channels = [];

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
        private ObservableCollection<PlcDevice> devices = [];

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
            return [.. Channels.Where(c => c.IsEnabled)];
        }

        /// <summary>
        /// 获取指定工步的通道
        /// </summary>
        public List<MeasurementChannel> GetChannelsByStep(int stepNumber)
        {
            return [.. Channels.Where(c => c.IsEnabled && c.StepNumber == stepNumber)];
        }

        //private void SubscribeBasicInfo(RecipeBasicInfoConfig config)
        //{
        //    config.PropertyChanged -= BasicInfo_PropertyChanged;
        //    config.PropertyChanged += BasicInfo_PropertyChanged;
        //}

        //private void SubscribeOtherSettings(RecipeOtherSettingsConfig config)
        //{
        //    config.PropertyChanged -= OtherSettings_PropertyChanged;
        //    config.PropertyChanged += OtherSettings_PropertyChanged;
        //}

        //private void BasicInfo_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        //{
        //    OnPropertyChanged(e.PropertyName);
        //}

        //private void OtherSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        //{
        //    OnPropertyChanged(e.PropertyName);
        //}
    }
}
