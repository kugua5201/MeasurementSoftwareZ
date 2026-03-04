using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.ViewModels;

namespace MeasurementSoftware.Models
{
    /// <summary>
    /// PLC数据传输模式
    /// </summary>
    public enum PlcTransferMode
    {
        /// <summary>
        /// TCP传输
        /// </summary>
        TCP,

        /// <summary>
        /// RS232串口传输
        /// </summary>
        RS232
    }

    /// <summary>
    /// PLC数据传输配置
    /// </summary>
    public partial class PlcDataTransferConfig : ObservableViewModel
    {
        /// <summary>
        /// 是否启用PLC数据传输
        /// </summary>
        [ObservableProperty]
        private bool isEnabled;

        /// <summary>
        /// 传输模式
        /// </summary>
        [ObservableProperty]
        private PlcTransferMode transferMode = PlcTransferMode.TCP;

        /// <summary>
        /// 目标PLC设备ID（0表示未关联）
        /// </summary>
        [ObservableProperty]
        private long targetPlcDeviceId;

        /// <summary>
        /// 是否传输每个通道数据
        /// </summary>
        [ObservableProperty]
        private bool transferChannelData = true;

        /// <summary>
        /// 是否传输总体结果
        /// </summary>
        [ObservableProperty]
        private bool transferOverallResult = true;

        /// <summary>
        /// 总体结果写入地址（PLC地址）
        /// </summary>
        [ObservableProperty]
        private string overallResultAddress = "DB1.DBW0";

        /// <summary>
        /// 是否传输测量完成信号
        /// </summary>
        [ObservableProperty]
        private bool transferCompleteSignal = true;

        /// <summary>
        /// 测量完成信号地址
        /// </summary>
        [ObservableProperty]
        private string completeSignalAddress = "DB1.DBX0.0";

        /// <summary>
        /// 传输超时（毫秒）
        /// </summary>
        [ObservableProperty]
        private int transferTimeout = 5000;

        /// <summary>
        /// 传输失败是否重试
        /// </summary>
        [ObservableProperty]
        private bool retryOnFailure = true;

        /// <summary>
        /// 重试次数
        /// </summary>
        [ObservableProperty]
        private int maxRetryCount = 3;
    }
}
