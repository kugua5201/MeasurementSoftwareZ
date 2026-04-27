using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace MeasurementSoftware.Models
{
    /// <summary>
    /// 虚拟测量引用的来源通道绑定项。
    /// 用于将其他测量通道结果映射为公式变量。
    /// </summary>
    public partial class VirtualMeasurementChannelBinding : ObservableObject
    {
        /// <summary>
        /// 公式变量名。
        /// 例如 X1、A、CH1 等。
        /// </summary>
        [ObservableProperty]
        private string sourceKey = string.Empty;

        /// <summary>
        /// 来源通道编号。
        /// 持久化时按通道编号保存，运行期再回填实际通道引用。
        /// </summary>
        [ObservableProperty]
        private int sourceChannelNumber;

        private MeasurementChannel? runtimeChannel;
        private PropertyChangedEventHandler? runtimeChannelPropertyChangedHandler;

        /// <summary>
        /// 运行时回填的来源通道引用。
        /// 仅用于编辑和运行时计算，不参与持久化。
        /// </summary>
        [JsonIgnore]
        public MeasurementChannel? RuntimeChannel
        {
            get => runtimeChannel;
            set
            {
                if (ReferenceEquals(runtimeChannel, value))
                {
                    return;
                }

                if (runtimeChannel != null && runtimeChannelPropertyChangedHandler != null)
                {
                    runtimeChannel.PropertyChanged -= runtimeChannelPropertyChangedHandler;
                }

                runtimeChannel = value;
                SourceChannelNumber = value?.ChannelNumber ?? 0;

                if (runtimeChannel != null)
                {
                    runtimeChannelPropertyChangedHandler = RuntimeChannel_PropertyChanged;
                    runtimeChannel.PropertyChanged += runtimeChannelPropertyChangedHandler;
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(SourceChannelName));
                OnPropertyChanged(nameof(SourceChannelAddress));
            }
        }

        /// <summary>
        /// 来源通道名称。
        /// 优先显示运行时通道名称，缺失时回退为编号文本。
        /// </summary>
        [JsonIgnore]
        public string SourceChannelName => RuntimeChannel?.ChannelName ?? (SourceChannelNumber <= 0 ? string.Empty : $"通道{SourceChannelNumber}");

        /// <summary>
        /// 来源通道地址摘要。
        /// </summary>
        [JsonIgnore]
        public string SourceChannelAddress => RuntimeChannel?.DisplayDataSourceAddress ?? string.Empty;

        /// <summary>
        /// 创建绑定项副本，供编辑态隔离原始配置。
        /// </summary>
        public VirtualMeasurementChannelBinding Clone()
        {
            return new VirtualMeasurementChannelBinding
            {
                SourceKey = SourceKey,
                SourceChannelNumber = SourceChannelNumber
            };
        }

        private void RuntimeChannel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(MeasurementChannel.ChannelName)
                or nameof(MeasurementChannel.DisplayDataSourceAddress)
                or nameof(MeasurementChannel.DisplayDataPointName)
                or nameof(MeasurementChannel.ChannelNumber))
            {
                OnPropertyChanged(nameof(SourceChannelName));
                OnPropertyChanged(nameof(SourceChannelAddress));
            }
        }
    }
}
