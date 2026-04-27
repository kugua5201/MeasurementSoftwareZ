using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.Extensions;
using MeasurementSoftware.ViewModels;
using MultiProtocol.Model;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;
namespace MeasurementSoftware.Models
{
    /// <summary>
    /// 测量通道模型
    /// </summary>
    public partial class MeasurementChannel : ObservableViewModel
    {
        public MeasurementChannel()
        {
            AttachIndirectSourceBindings(IndirectSourceBindings);
            EnsureVirtualSourceBindings(1);
            AttachVirtualSourceBindings(VirtualSourceBindings);
        }

        /// <summary>
        /// 通道编号
        /// </summary>
        [ObservableProperty]
        private int channelNumber;

        /// <summary>
        /// 通道名称
        /// </summary>
        [ObservableProperty]
        private string channelName = string.Empty;

        /// <summary>
        /// 通道说明
        /// </summary>
        [ObservableProperty]
        private string channelDescription = string.Empty;

        /// <summary>
        /// 通道类型
        /// </summary>
        [ObservableProperty]
        private ChannelType channelType = ChannelType.结果值;

        /// <summary>
        /// 通道模式。
        /// 用于区分直接测量、间接测量和虚拟通道。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDirectMeasurementMode))]
        [NotifyPropertyChangedFor(nameof(IsIndirectMeasurementMode))]
        [NotifyPropertyChangedFor(nameof(IsVirtualMeasurementMode))]
        [NotifyPropertyChangedFor(nameof(CanEditStepConfiguration))]
        [NotifyPropertyChangedFor(nameof(DisplayStepText))]
        [NotifyPropertyChangedFor(nameof(DisplayPlcDeviceName))]
        [NotifyPropertyChangedFor(nameof(DisplayDataPointName))]
        [NotifyPropertyChangedFor(nameof(DisplayDataSourceAddress))]
        private MeasurementChannelMode measurementMode = MeasurementChannelMode.Direct;

        /// <summary>
        /// 测量类型
        /// </summary>
        [ObservableProperty]
        private string measurementType = string.Empty;

        /// <summary>
        /// 标准值
        /// </summary>
        [ObservableProperty]
        private double standardValue;

        /// <summary>
        /// 公差上限
        /// </summary>
        [ObservableProperty]
        private double upperTolerance;

        /// <summary>
        /// 公差下限
        /// </summary>
        [ObservableProperty]
        private double lowerTolerance;


        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayMeasuredValue))]
        private double measuredValue;

        partial void OnMeasuredValueChanging(double value)
        {
            measuredValue = Math.Round(value, DecimalPlaces);
        }

        /// <summary>
        /// 测量结果（合格/不合格）
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayResultText))]
        private MeasurementResult result;

        /// <summary>
        /// 数据源地址（PLC地址）
        /// </summary>
        [ObservableProperty]
        private string dataSourceAddress = string.Empty;

        /// <summary>
        /// 关联的PLC设备ID（0表示未关联）
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PlcDeviceName))]
        private long plcDeviceId;

        /// <summary>
        /// 关联的数据点ID
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DataPointName))]
        private string dataPointId = string.Empty;

        /// <summary>
        /// 可用的数据点列表（根据选择的PLC设备动态加载）
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DataPointName))]
        private ObservableCollection<DataPoint> availableDataPoints = new();

        /// <summary>
        /// 间接测量公式脚本。
        /// 支持单行表达式，也支持多行“变量 = 表达式”的脚本写法。
        /// 多行脚本建议最后一行输出 RESULT。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayDataSourceAddress))]
        private string indirectFormula = string.Empty;

        /// <summary>
        /// 间接测量数据源列表。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayPlcDeviceName))]
        [NotifyPropertyChangedFor(nameof(DisplayDataPointName))]
        [NotifyPropertyChangedFor(nameof(DisplayDataSourceAddress))]
        private ObservableCollection<MeasurementChannelSourceBinding> indirectSourceBindings = [];

        /// <summary>
        /// 间接测量触发模式。
        /// 默认要求所有公式变量相对上一次已计算快照都发生变化后，才再次计算并存储。
        /// </summary>
        [ObservableProperty]
        private IndirectMeasurementTriggerMode indirectTriggerMode = IndirectMeasurementTriggerMode.AllValuesChanged;

        /// <summary>
        /// 虚拟测量来源模式。
        /// 用于在软件模拟数据与测量通道公式之间切换。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsChannelFormula))]
        [NotifyPropertyChangedFor(nameof(DisplayStepText))]
        [NotifyPropertyChangedFor(nameof(CanEditStepConfiguration))]
        private VirtualMeasurementSourceMode virtualSourceMode = VirtualMeasurementSourceMode.SoftwareSimulation;

        /// <summary>
        /// 是否通道测量模式
        /// </summary>
        [JsonIgnore]
        public bool IsChannelFormula => VirtualSourceMode == VirtualMeasurementSourceMode.ChannelFormula;

        /// <summary>
        /// 测量界面显示用工步文本。
        /// 虚拟通道的测量通道计算模式不受工步控制，统一显示为 --。
        /// </summary>
        [JsonIgnore]
        public string DisplayStepText => IsVirtualMeasurementMode && IsChannelFormula
            ? "--"
            : StepNumber.ToString();

        /// <summary>
        /// 是否允许编辑工步配置。
        /// 虚拟通道的测量通道计算模式不允许编辑工步，软件模拟模式不受影响。
        /// </summary>
        [JsonIgnore]
        public bool CanEditStepConfiguration => !(IsVirtualMeasurementMode && IsChannelFormula);

        /// <summary>
        /// 虚拟测量软件模拟波形类型。
        /// </summary>
        [ObservableProperty]
        private VirtualMeasurementWaveformType virtualWaveformType = VirtualMeasurementWaveformType.Sine;

        /// <summary>
        /// 虚拟测量模拟波形幅值。
        /// </summary>
        [ObservableProperty]
        private double virtualWaveformAmplitude = 1d;

        /// <summary>
        /// 虚拟测量模拟波形周期（秒）。
        /// </summary>
        [ObservableProperty]
        private double virtualWaveformPeriodSeconds = 1d;

        /// <summary>
        /// 虚拟测量方波占空比。
        /// 取值范围 0~1。
        /// </summary>
        [ObservableProperty]
        private double virtualWaveformDutyCycle = 0.5d;

        /// <summary>
        /// 虚拟测量软件模拟偏移量。
        /// 模拟值会在基础波形结果上叠加该偏移。
        /// </summary>
        [ObservableProperty]
        private double virtualWaveformOffset;

        /// <summary>
        /// 虚拟测量公式脚本。
        /// 用于基于其他测量通道结果进行公式计算。
        /// </summary>
        [ObservableProperty]
        private string virtualFormula = string.Empty;

        /// <summary>
        /// 虚拟测量来源通道绑定列表。
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<VirtualMeasurementChannelBinding> virtualSourceBindings = [];

        /// <summary>
        /// 是否启用测量结果输出。
        /// 启用后会在测量完成时将当前通道 OK/NG 结果写入指定 PLC 点位。
        /// </summary>
        [ObservableProperty]
        private bool enableResultOutput;

        /// <summary>
        /// 测量结果输出目标 PLC 设备 ID。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ResultOutputPlcDeviceName))]
        private long resultOutputPlcDeviceId;

        /// <summary>
        /// 测量结果输出目标点位 ID。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ResultOutputDataPointName))]
        private string resultOutputDataPointId = string.Empty;

        /// <summary>
        /// 测量结果输出地址。
        /// </summary>
        [ObservableProperty]
        private string resultOutputAddress = string.Empty;

        /// <summary>
        /// 输出 OK 时写入的值。
        /// Bool 点位会自动使用 True。
        /// </summary>
        [ObservableProperty]
        private string resultOutputOkValue = "1";

        /// <summary>
        /// 输出 NG 时写入的值。
        /// Bool 点位会自动使用 False。
        /// </summary>
        [ObservableProperty]
        private string resultOutputNgValue = "0";

        /// <summary>
        /// 结果输出可用点位列表。
        /// 根据输出设备动态联动。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ResultOutputDataPointName))]
        private ObservableCollection<DataPoint> availableResultOutputDataPoints = new();

        /// <summary>
        /// 运行时绑定的 PLC 设备实例。
        /// 仅在程序运行期使用，不参与配方持久化。
        /// </summary>
        private PlcDevice? runtimeDevice;
        private PropertyChangedEventHandler? runtimeDevicePropertyChangedHandler;
        private NotifyCollectionChangedEventHandler? runtimeDeviceDataPointsCollectionChangedHandler;

        [JsonIgnore]
        public PlcDevice? RuntimeDevice
        {
            get => runtimeDevice;
            set
            {
                var oldDevice = runtimeDevice;
                if (ReferenceEquals(runtimeDevice, value))
                {
                    return;
                }

                if (oldDevice != null && runtimeDevicePropertyChangedHandler != null)
                {
                    oldDevice.PropertyChanged -= runtimeDevicePropertyChangedHandler;
                }

                if (oldDevice?.DataPoints is INotifyCollectionChanged oldDataPointsCollection && runtimeDeviceDataPointsCollectionChangedHandler != null)
                {
                    oldDataPointsCollection.CollectionChanged -= runtimeDeviceDataPointsCollectionChangedHandler;
                }

                UnsubscribeFromAvailableDataPoints(AvailableDataPoints);

                runtimeDevice = value;

                if (oldDevice != null && oldDevice.DeviceId != value?.DeviceId)
                {
                    RuntimeDataPoint = null;
                    UseCacheValue = false;
                }

                PlcDeviceId = value?.DeviceId ?? 0;
                RefreshAvailableDataPoints();

                if (runtimeDataPoint == null || !AvailableDataPoints.Contains(runtimeDataPoint))
                {
                    RuntimeDataPoint = AvailableDataPoints.FirstOrDefault(dp => dp.PointId == DataPointId);
                }

                if (runtimeDevice != null)
                {
                    runtimeDevicePropertyChangedHandler = RuntimeDevice_PropertyChanged;
                    runtimeDevice.PropertyChanged += runtimeDevicePropertyChangedHandler;

                    runtimeDeviceDataPointsCollectionChangedHandler = RuntimeDeviceDataPoints_CollectionChanged;
                    if (runtimeDevice.DataPoints is INotifyCollectionChanged dataPointsCollection)
                    {
                        dataPointsCollection.CollectionChanged += runtimeDeviceDataPointsCollectionChangedHandler;
                    }
                }

                OnPropertyChanged(nameof(RuntimeDevice));
                OnPropertyChanged(nameof(PlcDeviceName));
                OnPropertyChanged(nameof(DataPointName));
                OnPropertyChanged(nameof(DisplayPlcDeviceName));
                OnPropertyChanged(nameof(DisplayDataPointName));
                OnPropertyChanged(nameof(DisplayDataSourceAddress));
            }
        }

        private void AttachVirtualSourceBindings(ObservableCollection<VirtualMeasurementChannelBinding>? bindings)
        {
            if (bindings == null)
            {
                return;
            }

            bindings.CollectionChanged -= VirtualSourceBindings_CollectionChanged;
            bindings.CollectionChanged += VirtualSourceBindings_CollectionChanged;

            foreach (var binding in bindings)
            {
                binding.PropertyChanged -= VirtualSourceBinding_PropertyChanged;
                binding.PropertyChanged += VirtualSourceBinding_PropertyChanged;
            }
        }

        private void VirtualSourceBindings_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (VirtualMeasurementChannelBinding binding in e.OldItems)
                {
                    binding.PropertyChanged -= VirtualSourceBinding_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (VirtualMeasurementChannelBinding binding in e.NewItems)
                {
                    binding.PropertyChanged -= VirtualSourceBinding_PropertyChanged;
                    binding.PropertyChanged += VirtualSourceBinding_PropertyChanged;
                }
            }

            OnPropertyChanged(nameof(DisplayPlcDeviceName));
            OnPropertyChanged(nameof(DisplayDataPointName));
            OnPropertyChanged(nameof(DisplayDataSourceAddress));
        }

        private void VirtualSourceBinding_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(VirtualMeasurementChannelBinding.SourceKey)
                or nameof(VirtualMeasurementChannelBinding.SourceChannelName)
                or nameof(VirtualMeasurementChannelBinding.SourceChannelAddress)
                or nameof(VirtualMeasurementChannelBinding.RuntimeChannel)
                or nameof(VirtualMeasurementChannelBinding.SourceChannelNumber))
            {
                OnPropertyChanged(nameof(DisplayPlcDeviceName));
                OnPropertyChanged(nameof(DisplayDataPointName));
                OnPropertyChanged(nameof(DisplayDataSourceAddress));
            }
        }

        /// <summary>
        /// 确保虚拟测量来源通道至少存在指定数量。
        /// </summary>
        public void EnsureVirtualSourceBindings(int minimumCount)
        {
            minimumCount = Math.Max(0, minimumCount);
            while (VirtualSourceBindings.Count < minimumCount)
            {
                var nextIndex = VirtualSourceBindings.Count + 1;
                VirtualSourceBindings.Add(new VirtualMeasurementChannelBinding
                {
                    SourceKey = $"X{nextIndex}"
                });
            }

            OnPropertyChanged(nameof(DisplayPlcDeviceName));
            OnPropertyChanged(nameof(DisplayDataPointName));
            OnPropertyChanged(nameof(DisplayDataSourceAddress));
        }

        /// <summary>
        /// 用新的虚拟测量来源通道集合替换当前集合。
        /// </summary>
        public void ReplaceVirtualSourceBindings(IEnumerable<VirtualMeasurementChannelBinding> bindings)
        {
            VirtualSourceBindings = new ObservableCollection<VirtualMeasurementChannelBinding>(bindings ?? []);
            AttachVirtualSourceBindings(VirtualSourceBindings);
            OnPropertyChanged(nameof(DisplayPlcDeviceName));
            OnPropertyChanged(nameof(DisplayDataPointName));
            OnPropertyChanged(nameof(DisplayDataSourceAddress));
        }

        /// <summary>
        /// 数据源显示用 PLC 设备名称。
        /// 直接测量显示单个设备，间接测量显示已绑定设备摘要。
        /// </summary>
        [JsonIgnore]
        public string DisplayPlcDeviceName => MeasurementMode switch
        {
            MeasurementChannelMode.Direct => PlcDeviceName,
            MeasurementChannelMode.Indirect => IndirectSourceBindings.Count == 0
                ? string.Empty
                : $"多设备/点位({IndirectSourceBindings.Count})",
            MeasurementChannelMode.Virtual => VirtualSourceMode == VirtualMeasurementSourceMode.SoftwareSimulation
                ? "软件模拟"
                : VirtualSourceBindings.Count == 0
                    ? string.Empty
                    : $"测量通道({VirtualSourceBindings.Count})",
            _ => string.Empty
        };

        /// <summary>
        /// 运行时绑定的结果输出 PLC 设备实例。
        /// 仅在程序运行期使用，不参与配方持久化。
        /// </summary>
        private PlcDevice? resultOutputRuntimeDevice;
        private PropertyChangedEventHandler? resultOutputRuntimeDevicePropertyChangedHandler;

        [JsonIgnore]
        public PlcDevice? ResultOutputRuntimeDevice
        {
            get => resultOutputRuntimeDevice;
            set
            {
                var oldDevice = resultOutputRuntimeDevice;
                if (ReferenceEquals(resultOutputRuntimeDevice, value))
                {
                    return;
                }

                if (oldDevice != null && resultOutputRuntimeDevicePropertyChangedHandler != null)
                {
                    oldDevice.PropertyChanged -= resultOutputRuntimeDevicePropertyChangedHandler;
                }

                resultOutputRuntimeDevice = value;

                if (oldDevice != null && oldDevice.DeviceId != value?.DeviceId)
                {
                    ResultOutputRuntimeDataPoint = null;
                }

                ResultOutputPlcDeviceId = value?.DeviceId ?? 0;
                RefreshAvailableResultOutputDataPoints();

                if (resultOutputRuntimeDataPoint == null || !AvailableResultOutputDataPoints.Contains(resultOutputRuntimeDataPoint))
                {
                    ResultOutputRuntimeDataPoint = AvailableResultOutputDataPoints.FirstOrDefault(dp => dp.PointId == ResultOutputDataPointId);
                }

                if (resultOutputRuntimeDevice != null)
                {
                    resultOutputRuntimeDevicePropertyChangedHandler = ResultOutputRuntimeDevice_PropertyChanged;
                    resultOutputRuntimeDevice.PropertyChanged += resultOutputRuntimeDevicePropertyChangedHandler;
                }

                OnPropertyChanged(nameof(ResultOutputRuntimeDevice));
                OnPropertyChanged(nameof(ResultOutputPlcDeviceName));
                OnPropertyChanged(nameof(ResultOutputDataPointName));
            }
        }

        /// <summary>
        /// 数据源显示用点位名称。
        /// </summary>
        [JsonIgnore]
        public string DisplayDataPointName => MeasurementMode switch
        {
            MeasurementChannelMode.Direct => DataPointName,
            MeasurementChannelMode.Indirect => string.Join("、", IndirectSourceBindings
                .Where(binding => !string.IsNullOrWhiteSpace(binding.SourceKey))
                .Select(binding => binding.SourceKey.Trim())),
            MeasurementChannelMode.Virtual => VirtualSourceMode == VirtualMeasurementSourceMode.SoftwareSimulation
                ? VirtualWaveformType.GetDescription()
                : string.Join("、", VirtualSourceBindings
                    .Where(binding => !string.IsNullOrWhiteSpace(binding.SourceKey))
                    .Select(binding => string.IsNullOrWhiteSpace(binding.SourceChannelName)
                        ? binding.SourceKey.Trim()
                        : $"{binding.SourceKey.Trim()}")),
            _ => string.Empty
        };

        /// <summary>
        /// 数据源显示用地址信息。
        /// </summary>
        [JsonIgnore]
        public string DisplayDataSourceAddress => MeasurementMode switch
        {
            MeasurementChannelMode.Direct => DataSourceAddress,
            MeasurementChannelMode.Indirect => string.Join("；", IndirectSourceBindings
                .Where(binding => !string.IsNullOrWhiteSpace(binding.DataSourceAddress))
                .Select(binding => string.IsNullOrWhiteSpace(binding.SourceKey)
                    ? binding.DataSourceAddress
                    : $"{binding.SourceKey}:{binding.DataSourceAddress}")),
            MeasurementChannelMode.Virtual => VirtualSourceMode == VirtualMeasurementSourceMode.SoftwareSimulation
                ? VirtualWaveformType == VirtualMeasurementWaveformType.Square
                    ? $"幅值:{VirtualWaveformAmplitude}；偏移:{VirtualWaveformOffset}；周期:{VirtualWaveformPeriodSeconds}ms；占空比:{VirtualWaveformDutyCycle:P0}"
                    : $"幅值:{VirtualWaveformAmplitude}；偏移:{VirtualWaveformOffset}；周期:{VirtualWaveformPeriodSeconds}ms"
                : string.Join("；", VirtualSourceBindings
                    .Where(binding => !string.IsNullOrWhiteSpace(binding.SourceKey))
                    .Select(binding => string.IsNullOrWhiteSpace(binding.SourceChannelAddress)
                        ? binding.SourceKey.Trim()
                        : $"{binding.SourceKey.Trim()}:{binding.SourceChannelName}")),
            _ => string.Empty
        };

        /// <summary>
        /// 是否为直接测量模式。
        /// </summary>
        [JsonIgnore]
        public bool IsDirectMeasurementMode => MeasurementMode == MeasurementChannelMode.Direct;

        /// <summary>
        /// 是否为间接测量模式。
        /// </summary>
        [JsonIgnore]
        public bool IsIndirectMeasurementMode => MeasurementMode == MeasurementChannelMode.Indirect;

        /// <summary>
        /// 是否为虚拟通道模式。
        /// </summary>
        [JsonIgnore]
        public bool IsVirtualMeasurementMode => MeasurementMode == MeasurementChannelMode.Virtual;

        /// <summary>
        /// 运行时绑定的结果输出点位实例。
        /// 仅在程序运行期使用，不参与配方持久化。
        /// </summary>
        private DataPoint? resultOutputRuntimeDataPoint;
        private PropertyChangedEventHandler? resultOutputRuntimeDataPointPropertyChangedHandler;

        [JsonIgnore]
        public DataPoint? ResultOutputRuntimeDataPoint
        {
            get => resultOutputRuntimeDataPoint;
            set
            {
                if (ReferenceEquals(resultOutputRuntimeDataPoint, value))
                {
                    return;
                }

                if (resultOutputRuntimeDataPoint != null && resultOutputRuntimeDataPointPropertyChangedHandler != null)
                {
                    resultOutputRuntimeDataPoint.PropertyChanged -= resultOutputRuntimeDataPointPropertyChangedHandler;
                }

                resultOutputRuntimeDataPoint = value;
                ResultOutputDataPointId = value?.PointId ?? string.Empty;
                ResultOutputAddress = value?.Address ?? string.Empty;

                if (resultOutputRuntimeDataPoint != null)
                {
                    resultOutputRuntimeDataPointPropertyChangedHandler = ResultOutputRuntimeDataPoint_PropertyChanged;
                    resultOutputRuntimeDataPoint.PropertyChanged += resultOutputRuntimeDataPointPropertyChangedHandler;
                }

                if (IsResultOutputBoolDataPoint)
                {
                    ResultOutputOkValue = bool.TrueString;
                    ResultOutputNgValue = bool.FalseString;
                }

                OnPropertyChanged(nameof(ResultOutputRuntimeDataPoint));
                OnPropertyChanged(nameof(ResultOutputDataPointName));
                OnPropertyChanged(nameof(IsResultOutputBoolDataPoint));
            }
        }

        /// <summary>
        /// 当前结果输出点位是否为 Bool 类型。
        /// </summary>
        [JsonIgnore]
        public bool IsResultOutputBoolDataPoint => ResultOutputRuntimeDataPoint?.DataType == FieldType.Bool;

        /// <summary>
        /// 运行时绑定的采集点位实例。
        /// 仅在程序运行期使用，不参与配方持久化。
        /// </summary>
        private DataPoint? runtimeDataPoint;
        private PropertyChangedEventHandler? runtimeDataPointPropertyChangedHandler;

        [JsonIgnore]
        public DataPoint? RuntimeDataPoint
        {
            get => runtimeDataPoint;
            set
            {
                if (ReferenceEquals(runtimeDataPoint, value))
                {
                    return;
                }

                if (runtimeDataPoint != null && runtimeDataPointPropertyChangedHandler != null)
                {
                    runtimeDataPoint.PropertyChanged -= runtimeDataPointPropertyChangedHandler;
                }

                runtimeDataPoint = value;
                DataPointId = value?.PointId ?? string.Empty;
                DataSourceAddress = value?.Address ?? string.Empty;

                if (runtimeDataPoint != null)
                {
                    runtimeDataPointPropertyChangedHandler = RuntimeDataPoint_PropertyChanged;
                    runtimeDataPoint.PropertyChanged += runtimeDataPointPropertyChangedHandler;
                }

                OnPropertyChanged(nameof(RuntimeDataPoint));
                OnPropertyChanged(nameof(DataPointName));
                OnPropertyChanged(nameof(DisplayDataPointName));
                OnPropertyChanged(nameof(DisplayDataSourceAddress));
            }
        }

        /// <summary>
        /// 按已保存的输出设备/点位标识回填结果输出运行时绑定。
        /// </summary>
        public void HydrateResultOutputBindings(PlcDevice? device)
        {
            if (resultOutputRuntimeDevice != null && resultOutputRuntimeDevicePropertyChangedHandler != null)
            {
                resultOutputRuntimeDevice.PropertyChanged -= resultOutputRuntimeDevicePropertyChangedHandler;
            }

            if (resultOutputRuntimeDataPoint != null && resultOutputRuntimeDataPointPropertyChangedHandler != null)
            {
                resultOutputRuntimeDataPoint.PropertyChanged -= resultOutputRuntimeDataPointPropertyChangedHandler;
            }

            resultOutputRuntimeDevice = device;

            AvailableResultOutputDataPoints = resultOutputRuntimeDevice == null
                ? []
                : new ObservableCollection<DataPoint>(resultOutputRuntimeDevice.DataPoints
                    .Where(dp => dp.IsEnabled)
                    .OrderBy(dp => int.TryParse(dp.PointId, out var id) ? id : int.MaxValue));

            resultOutputRuntimeDataPoint = AvailableResultOutputDataPoints.FirstOrDefault(dp => dp.PointId == ResultOutputDataPointId);

            if (resultOutputRuntimeDevice != null)
            {
                resultOutputRuntimeDevicePropertyChangedHandler = ResultOutputRuntimeDevice_PropertyChanged;
                resultOutputRuntimeDevice.PropertyChanged += resultOutputRuntimeDevicePropertyChangedHandler;
            }

            if (resultOutputRuntimeDataPoint != null)
            {
                resultOutputRuntimeDataPointPropertyChangedHandler = ResultOutputRuntimeDataPoint_PropertyChanged;
                resultOutputRuntimeDataPoint.PropertyChanged += resultOutputRuntimeDataPointPropertyChangedHandler;
                ResultOutputAddress = resultOutputRuntimeDataPoint.Address;
            }

            if (IsResultOutputBoolDataPoint)
            {
                ResultOutputOkValue = bool.TrueString;
                ResultOutputNgValue = bool.FalseString;
            }

            OnPropertyChanged(nameof(ResultOutputRuntimeDevice));
            OnPropertyChanged(nameof(ResultOutputRuntimeDataPoint));
            OnPropertyChanged(nameof(ResultOutputPlcDeviceName));
            OnPropertyChanged(nameof(ResultOutputDataPointName));
            OnPropertyChanged(nameof(IsResultOutputBoolDataPoint));
        }

        /// <summary>
        /// 按已保存的设备/点位标识回填运行时绑定。
        /// 仅刷新运行时引用，不修改持久化的设备、点位与地址字段。
        /// </summary>
        public void HydrateRuntimeBindings(PlcDevice? device)
        {
            if (runtimeDevice != null && runtimeDevicePropertyChangedHandler != null)
            {
                runtimeDevice.PropertyChanged -= runtimeDevicePropertyChangedHandler;
            }

            if (runtimeDevice?.DataPoints is INotifyCollectionChanged oldDataPointsCollection && runtimeDeviceDataPointsCollectionChangedHandler != null)
            {
                oldDataPointsCollection.CollectionChanged -= runtimeDeviceDataPointsCollectionChangedHandler;
            }

            if (runtimeDataPoint != null && runtimeDataPointPropertyChangedHandler != null)
            {
                runtimeDataPoint.PropertyChanged -= runtimeDataPointPropertyChangedHandler;
            }

            UnsubscribeFromAvailableDataPoints(AvailableDataPoints);

            runtimeDevice = device;

            AvailableDataPoints = runtimeDevice == null
                ? []
                : new ObservableCollection<DataPoint>(runtimeDevice.DataPoints
                    .Where(dp => dp.IsEnabled)
                    .OrderBy(dp => int.TryParse(dp.PointId, out var id) ? id : int.MaxValue));
            SubscribeToAvailableDataPoints(AvailableDataPoints);

            runtimeDataPoint = AvailableDataPoints.FirstOrDefault(dp => dp.PointId == DataPointId);

            if (runtimeDevice != null)
            {
                runtimeDevicePropertyChangedHandler = RuntimeDevice_PropertyChanged;
                runtimeDevice.PropertyChanged += runtimeDevicePropertyChangedHandler;

                runtimeDeviceDataPointsCollectionChangedHandler = RuntimeDeviceDataPoints_CollectionChanged;
                if (runtimeDevice.DataPoints is INotifyCollectionChanged dataPointsCollection)
                {
                    dataPointsCollection.CollectionChanged += runtimeDeviceDataPointsCollectionChangedHandler;
                }
            }

            if (runtimeDataPoint != null)
            {
                runtimeDataPointPropertyChangedHandler = RuntimeDataPoint_PropertyChanged;
                runtimeDataPoint.PropertyChanged += runtimeDataPointPropertyChangedHandler;
                DataSourceAddress = runtimeDataPoint.Address;
            }

            OnPropertyChanged(nameof(RuntimeDevice));
            OnPropertyChanged(nameof(RuntimeDataPoint));
            OnPropertyChanged(nameof(PlcDeviceName));
            OnPropertyChanged(nameof(DataPointName));
            OnPropertyChanged(nameof(DisplayPlcDeviceName));
            OnPropertyChanged(nameof(DisplayDataPointName));
            OnPropertyChanged(nameof(DisplayDataSourceAddress));
        }

        /// <summary>
        /// PLC设备名称（用于显示）
        /// </summary>
        public string PlcDeviceName
        {
            get
            {
                if (PlcDeviceId == 0)
                    return string.Empty;

                return RuntimeDevice?.DeviceName ?? PlcDeviceId.ToString();
            }
        }

        /// <summary>
        /// 结果输出 PLC 设备名称。
        /// </summary>
        public string ResultOutputPlcDeviceName
        {
            get
            {
                if (ResultOutputPlcDeviceId == 0)
                    return string.Empty;

                return ResultOutputRuntimeDevice?.DeviceName ?? ResultOutputPlcDeviceId.ToString();
            }
        }

        /// <summary>
        /// 结果输出点位名称。
        /// </summary>
        public string ResultOutputDataPointName
        {
            get
            {
                if (string.IsNullOrEmpty(ResultOutputDataPointId))
                    return string.Empty;

                if (ResultOutputRuntimeDataPoint != null)
                    return ResultOutputRuntimeDataPoint.PointName;

                var point = AvailableResultOutputDataPoints?.FirstOrDefault(p => p.PointId == ResultOutputDataPointId);
                return point?.PointName ?? ResultOutputDataPointId;
            }
        }

        /// <summary>
        /// 数据点名称（用于显示）
        /// </summary>
        public string DataPointName
        {
            get
            {
                if (string.IsNullOrEmpty(DataPointId))
                    return string.Empty;

                if (RuntimeDataPoint != null)
                    return RuntimeDataPoint.PointName;

                // 从可用数据点列表中查找点位名称
                var point = AvailableDataPoints?.FirstOrDefault(p => p.PointId == DataPointId);
                return point?.PointName ?? DataPointId;
            }
        }

        /// <summary>
        /// 是否启用
        /// </summary>
        [ObservableProperty]
        private bool isEnabled = true;

        /// <summary>
        /// 单位
        /// </summary>
        [ObservableProperty]
        private string unit = string.Empty;

        /// <summary>
        /// 小数位数
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayMeasuredValue))]
        [NotifyPropertyChangedFor(nameof(DisplayResultValue))]
        private int decimalPlaces = 3;

        /// <summary>
        /// 是否需要校准
        /// </summary>
        [ObservableProperty]
        private bool requiresCalibration;

        /// <summary>
        /// 当前生效的校准方式
        /// </summary>
        [ObservableProperty]
        private CalibrationMode calibrationMode = CalibrationMode.SinglePoint;

        /// <summary>
        /// 校准系数 A（线性公式：y = Ax + B）
        /// </summary>
        [ObservableProperty]
        private double calibrationCoefficientA = 1.0;

        /// <summary>
        /// 校准系数 B（线性公式：y = Ax + B）
        /// </summary>
        [ObservableProperty]
        private double calibrationCoefficientB = 0.0;

        /// <summary>
        /// 上次校准时间
        /// </summary>
        [ObservableProperty]
        private DateTime? lastCalibrationTime;


        /// <summary>
        /// 单点校准配置
        /// </summary>
        [ObservableProperty]
        private SinglePointCalibrationSettings singlePointCalibration = new();

        /// <summary>
        /// 最小二乘法校准配置
        /// </summary>
        [ObservableProperty]
        private LeastSquaresCalibrationSettings leastSquaresCalibration = new();

        /// <summary>
        /// 线性回归校准配置
        /// </summary>
        [ObservableProperty]
        private LinearRegressionCalibrationSettings linearRegressionCalibration = new();

        /// <summary>
        /// 校准历史（随配方保存）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<CalibrationRecord> calibrationHistory = [];

        /// <summary>
        /// 工步编号（用于分步测量）
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayStepText))]
        private int stepNumber = 1;

        /// <summary>
        /// 工步名称
        /// </summary>
        [ObservableProperty]
        private string stepName = "默认工步";

        /// <summary>
        /// 通道标注点（在产品图片上标注测量位置，每个通道最多一个标注）
        /// </summary>
        [ObservableProperty]
        private ChannelAnnotation? annotation;

        /// <summary>
        /// 是否使用缓存值（仅适用于 S7-1200/1500 启用缓存的点位）
        /// true = 读取缓存解析值，false = 读取寄存器实时值
        /// </summary>
        [ObservableProperty]
        private bool useCacheValue;

        /// <summary>
        /// 采样数量（缓存数据大小，用于计算最大值、最小值、跳动等）
        /// </summary>
        [ObservableProperty]
        private int sampleCount = 100;

        /// <summary>
        /// 实时值是否有效。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayMeasuredValue))]
        private bool isMeasuredValueAvailable;

        [JsonIgnore]
        /// <summary>
        /// 历史数据（用于计算最大值、最小值等）
        /// </summary>
        public List<double> HistoricalData { get; set; } = [];

        /// <summary>
        /// 绑定运行时设备实例，并同步可用点位集合。
        /// </summary>
        public void BindDevice(PlcDevice? device)
        {
            RuntimeDevice = device;
        }

        /// <summary>
        /// 绑定运行时采集点位实例。
        /// </summary>
        public void BindDataPoint(DataPoint? dataPoint)
        {
            RuntimeDataPoint = dataPoint;
        }

        /// <summary>
        /// 绑定结果输出设备。
        /// </summary>
        public void BindResultOutputDevice(PlcDevice? device)
        {
            ResultOutputRuntimeDevice = device;
        }

        /// <summary>
        /// 绑定结果输出点位。
        /// </summary>
        public void BindResultOutputDataPoint(DataPoint? dataPoint)
        {
            ResultOutputRuntimeDataPoint = dataPoint;
        }

        /// <summary>
        /// 清空运行时设备与点位绑定。
        /// </summary>
        public void ClearRuntimeBindings()
        {
            RuntimeDevice = null;
            RuntimeDataPoint = null;
            PlcDeviceId = 0;
            DataPointId = string.Empty;
            DataSourceAddress = string.Empty;
            UnsubscribeFromAvailableDataPoints(AvailableDataPoints);
            AvailableDataPoints = [];
            UseCacheValue = false;
            OnPropertyChanged(nameof(PlcDeviceName));
            OnPropertyChanged(nameof(DataPointName));
            OnPropertyChanged(nameof(DisplayPlcDeviceName));
            OnPropertyChanged(nameof(DisplayDataPointName));
            OnPropertyChanged(nameof(DisplayDataSourceAddress));
        }

        /// <summary>
        /// 清空结果输出运行时绑定。
        /// </summary>
        public void ClearResultOutputBindings()
        {
            ResultOutputRuntimeDevice = null;
            ResultOutputRuntimeDataPoint = null;
            ResultOutputPlcDeviceId = 0;
            ResultOutputDataPointId = string.Empty;
            ResultOutputAddress = string.Empty;
            AvailableResultOutputDataPoints = [];
            OnPropertyChanged(nameof(ResultOutputPlcDeviceName));
            OnPropertyChanged(nameof(ResultOutputDataPointName));
            OnPropertyChanged(nameof(IsResultOutputBoolDataPoint));
        }

        private void RuntimeDevice_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlcDevice.IsEnabled) && runtimeDevice?.IsEnabled != true)
            {
                HydrateRuntimeBindings(null);
                return;
            }

            if (e.PropertyName is nameof(PlcDevice.DeviceName) or nameof(PlcDevice.DeviceId) or nameof(PlcDevice.IsEnabled))
            {
                RefreshAvailableDataPoints();
                OnPropertyChanged(nameof(RuntimeDevice));
                OnPropertyChanged(nameof(PlcDeviceName));
            }
        }

        private void RuntimeDeviceDataPoints_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (DataPoint dataPoint in e.OldItems)
                {
                    dataPoint.PropertyChanged -= RuntimeDataPointSource_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (DataPoint dataPoint in e.NewItems)
                {
                    dataPoint.PropertyChanged -= RuntimeDataPointSource_PropertyChanged;
                    dataPoint.PropertyChanged += RuntimeDataPointSource_PropertyChanged;
                }
            }

            RefreshAvailableDataPoints();
        }

        private void RuntimeDataPointSource_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not DataPoint dataPoint)
            {
                return;
            }

            if (ReferenceEquals(runtimeDataPoint, dataPoint) && e.PropertyName == nameof(DataPoint.PointId))
            {
                DataPointId = dataPoint.PointId;
            }

            if (ReferenceEquals(runtimeDataPoint, dataPoint) && e.PropertyName == nameof(DataPoint.Address))
            {
                DataSourceAddress = dataPoint.Address;
            }

            if (e.PropertyName is nameof(DataPoint.IsEnabled) or nameof(DataPoint.PointId) or nameof(DataPoint.PointName) or nameof(DataPoint.Address))
            {
                RefreshAvailableDataPoints();
            }
        }

        private void RuntimeDataPoint_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(DataPoint.PointId) or nameof(DataPoint.PointName) or nameof(DataPoint.Address))
            {
                if (sender is DataPoint point)
                {
                    DataPointId = point.PointId;
                    DataSourceAddress = point.Address;
                }

                OnPropertyChanged(nameof(DataPointName));
                OnPropertyChanged(nameof(DisplayPlcDeviceName));
                OnPropertyChanged(nameof(DisplayDataPointName));
                OnPropertyChanged(nameof(DisplayDataSourceAddress));
            }
        }

        public void RefreshAvailableDataPoints()
        {
            UnsubscribeFromAvailableDataPoints(AvailableDataPoints);
            AvailableDataPoints = RuntimeDevice == null
                ? []
                : new ObservableCollection<DataPoint>(RuntimeDevice.DataPoints
                    .Where(dp => dp.IsEnabled)
                    .OrderBy(dp => int.TryParse(dp.PointId, out var id) ? id : int.MaxValue));
            SubscribeToAvailableDataPoints(AvailableDataPoints);

            RuntimeDataPoint = AvailableDataPoints.FirstOrDefault(dp => dp.PointId == DataPointId);
            if (RuntimeDataPoint != null)
            {
                DataSourceAddress = RuntimeDataPoint.Address;
            }

            OnPropertyChanged(nameof(DataPointName));
            OnPropertyChanged(nameof(DisplayDataPointName));
            OnPropertyChanged(nameof(DisplayDataSourceAddress));
        }

        /// <summary>
        /// 确保间接测量数据源至少存在指定数量。
        /// </summary>
        public void EnsureIndirectSourceBindings(int minimumCount)
        {
            minimumCount = Math.Max(0, minimumCount);
            while (IndirectSourceBindings.Count < minimumCount)
            {
                var nextIndex = IndirectSourceBindings.Count + 1;
                IndirectSourceBindings.Add(new MeasurementChannelSourceBinding
                {
                    SourceKey = $"X{nextIndex}"
                });
            }

            OnPropertyChanged(nameof(DisplayPlcDeviceName));
            OnPropertyChanged(nameof(DisplayDataPointName));
            OnPropertyChanged(nameof(DisplayDataSourceAddress));
        }

        /// <summary>
        /// 用新的间接测量数据源集合替换当前集合。
        /// </summary>
        public void ReplaceIndirectSourceBindings(IEnumerable<MeasurementChannelSourceBinding> bindings)
        {
            DetachIndirectSourceBindings(IndirectSourceBindings);
            IndirectSourceBindings = new ObservableCollection<MeasurementChannelSourceBinding>(bindings ?? []);
            AttachIndirectSourceBindings(IndirectSourceBindings);
            OnPropertyChanged(nameof(DisplayPlcDeviceName));
            OnPropertyChanged(nameof(DisplayDataPointName));
            OnPropertyChanged(nameof(DisplayDataSourceAddress));
        }

        private void AttachIndirectSourceBindings(ObservableCollection<MeasurementChannelSourceBinding>? bindings)
        {
            if (bindings == null)
            {
                return;
            }

            bindings.CollectionChanged -= IndirectSourceBindings_CollectionChanged;
            bindings.CollectionChanged += IndirectSourceBindings_CollectionChanged;

            foreach (var binding in bindings)
            {
                binding.PropertyChanged -= IndirectSourceBinding_PropertyChanged;
                binding.PropertyChanged += IndirectSourceBinding_PropertyChanged;
            }
        }

        private void DetachIndirectSourceBindings(ObservableCollection<MeasurementChannelSourceBinding>? bindings)
        {
            if (bindings == null)
            {
                return;
            }

            bindings.CollectionChanged -= IndirectSourceBindings_CollectionChanged;

            foreach (var binding in bindings)
            {
                binding.PropertyChanged -= IndirectSourceBinding_PropertyChanged;
            }
        }

        private void IndirectSourceBindings_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (MeasurementChannelSourceBinding binding in e.OldItems)
                {
                    binding.PropertyChanged -= IndirectSourceBinding_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (MeasurementChannelSourceBinding binding in e.NewItems)
                {
                    binding.PropertyChanged -= IndirectSourceBinding_PropertyChanged;
                    binding.PropertyChanged += IndirectSourceBinding_PropertyChanged;
                }
            }

            OnPropertyChanged(nameof(DisplayPlcDeviceName));
            OnPropertyChanged(nameof(DisplayDataPointName));
            OnPropertyChanged(nameof(DisplayDataSourceAddress));
        }

        private void IndirectSourceBinding_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(MeasurementChannelSourceBinding.SourceKey)
                or nameof(MeasurementChannelSourceBinding.DataSourceAddress)
                or nameof(MeasurementChannelSourceBinding.DataPointId)
                or nameof(MeasurementChannelSourceBinding.RuntimeDataPoint)
                or nameof(MeasurementChannelSourceBinding.RuntimeDevice))
            {
                OnPropertyChanged(nameof(DisplayPlcDeviceName));
                OnPropertyChanged(nameof(DisplayDataPointName));
                OnPropertyChanged(nameof(DisplayDataSourceAddress));
            }
        }

        private void SubscribeToAvailableDataPoints(IEnumerable<DataPoint> dataPoints)
        {
            foreach (var dataPoint in dataPoints)
            {
                dataPoint.PropertyChanged -= RuntimeDataPointSource_PropertyChanged;
                dataPoint.PropertyChanged += RuntimeDataPointSource_PropertyChanged;
            }
        }

        private void UnsubscribeFromAvailableDataPoints(IEnumerable<DataPoint> dataPoints)
        {
            foreach (var dataPoint in dataPoints)
            {
                dataPoint.PropertyChanged -= RuntimeDataPointSource_PropertyChanged;
            }
        }

        private void ResultOutputRuntimeDevice_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlcDevice.DeviceName))
            {
                OnPropertyChanged(nameof(ResultOutputPlcDeviceName));
            }

            if (e.PropertyName == nameof(PlcDevice.IsEnabled) && sender is PlcDevice device)
            {
                if (!device.IsEnabled)
                {
                    ClearResultOutputBindings();
                    return;
                }

                RefreshAvailableResultOutputDataPoints();
                OnPropertyChanged(nameof(ResultOutputPlcDeviceName));
            }
        }

        private void ResultOutputRuntimeDataPoint_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(DataPoint.PointId) or nameof(DataPoint.PointName) or nameof(DataPoint.Address) or nameof(DataPoint.DataType))
            {
                if (sender is DataPoint point)
                {
                    ResultOutputDataPointId = point.PointId;
                    ResultOutputAddress = point.Address;
                }

                if (IsResultOutputBoolDataPoint)
                {
                    ResultOutputOkValue = bool.TrueString;
                    ResultOutputNgValue = bool.FalseString;
                }

                OnPropertyChanged(nameof(ResultOutputDataPointName));
                OnPropertyChanged(nameof(IsResultOutputBoolDataPoint));
            }
        }

        /// <summary>
        /// 根据当前结果输出设备刷新可用输出点位列表，并回填运行时输出点位引用。
        /// </summary>
        public void RefreshAvailableResultOutputDataPoints()
        {
            AvailableResultOutputDataPoints = ResultOutputRuntimeDevice == null ? [] : new ObservableCollection<DataPoint>(ResultOutputRuntimeDevice.DataPoints
                    .Where(dp => dp.IsEnabled)
                    .OrderBy(dp => int.TryParse(dp.PointId, out var id) ? id : int.MaxValue));

            ResultOutputRuntimeDataPoint = AvailableResultOutputDataPoints.FirstOrDefault(dp => dp.PointId == ResultOutputDataPointId);
            if (ResultOutputRuntimeDataPoint != null)
            {
                ResultOutputAddress = ResultOutputRuntimeDataPoint.Address;
            }

            OnPropertyChanged(nameof(ResultOutputDataPointName));
            OnPropertyChanged(nameof(IsResultOutputBoolDataPoint));
        }

        /// <summary>
        /// 检查测量结果是否合格
        /// </summary>
        public void CheckResult()
        {
            var upperLimit = StandardValue + UpperTolerance;
            var lowerLimit = StandardValue - LowerTolerance;
            var valueToCheck = IsResultValueAvailable ? ReusltValue : MeasuredValue;

            if (valueToCheck >= lowerLimit && valueToCheck <= upperLimit)
            {
                Result = MeasurementResult.Pass;
            }
            else
            {
                Result = MeasurementResult.Fail;
            }
        }

        private readonly object _dataLock = new();

        /// <summary>
        /// 根据通道类型处理并更新测量值
        /// </summary>
        /// <param name="rawValue">原始值</param>
        public void UpdateMeasuredValue(double rawValue)
        {

            lock (_dataLock)

            {
                IsMeasuredValueAvailable = true;
                MeasuredValue = rawValue;
                var checkValue = RoundMeasuredValue(rawValue);
                TrimHistoricalDataForIncoming(SampleCount, 1);
                HistoricalData.Add(checkValue);

            }
        }

        /// <summary>
        /// 硬件缓存时加入硬件缓存值，并且刷新实时值
        /// </summary>
        /// <param name="rawValues"></param>
        /// <param name="rawValue"></param>
        public void AppendMeasuredValues(IEnumerable<double> rawValues, double rawValue)
        {
            lock (_dataLock)
            {
                IsMeasuredValueAvailable = true;
                MeasuredValue = rawValue;
                if (rawValues is IReadOnlyList<double> rawValueList)
                {
                    int startIndex = Math.Max(0, rawValueList.Count - SampleCount);
                    int incomingCount = rawValueList.Count - startIndex;
                    TrimHistoricalDataForIncoming(SampleCount, incomingCount);
                    int requiredCapacity = HistoricalData.Count + incomingCount;
                    if (HistoricalData.Capacity < requiredCapacity)
                    {
                        HistoricalData.Capacity = requiredCapacity;
                    }
                    for (int i = startIndex; i < rawValueList.Count; i++)
                    {
                        double lastMeasuredValue = RoundMeasuredValue(rawValueList[i]);
                        HistoricalData.Add(lastMeasuredValue);
                    }
                }

            }
        }

        /// <summary>
        /// 去掉历史数据
        /// </summary>
        private void TrimHistoricalData()
        {
            int maxSamples = Math.Max(1, SampleCount);
            if (HistoricalData.Count > maxSamples)
            {
                HistoricalData.RemoveRange(0, HistoricalData.Count - maxSamples);
            }
        }

        /// <summary>
        /// 去掉历史数据中超出容量限制的旧数据，以便为即将追加的新数据腾出空间。
        /// </summary>
        /// <param name="maxSamples">历史数据的最大容量</param>
        /// <param name="incomingCount">即将追加的新数据数量</param>
        private void TrimHistoricalDataForIncoming(int maxSamples, int incomingCount)
        {
            if (incomingCount >= maxSamples)
            {
                HistoricalData.Clear();
                return;
            }

            int overflow = HistoricalData.Count + incomingCount - maxSamples;
            if (overflow > 0)
            {
                HistoricalData.RemoveRange(0, overflow);
            }
        }

        /// <summary>
        /// 通过校准并且转换保留对应的值
        /// </summary>
        /// <param name="rawValue">原始测量值</param>
        /// <param name="applyCalibration">是否应用校准</param>
        /// <returns>经过校准和小数位处理后的测量值</returns>
        private double RoundMeasuredValue(double rawValue)
        {
            if (RequiresCalibration)
            {
                rawValue = CalibrationCoefficientA * rawValue + CalibrationCoefficientB;
            }

            return Math.Round(rawValue, DecimalPlaces);
        }


        /// <summary>
        /// 最终结果
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayResultValue))]
        private double reusltValue;

        /// <summary>
        /// 结果值是否有效。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayResultValue))]
        private bool isResultValueAvailable;

        /// <summary>
        /// 通道显示状态。
        /// </summary>
        [ObservableProperty]
        private MeasurementResult displayState = MeasurementResult.Waiting;

        [JsonIgnore]
        public string DisplayMeasuredValue => IsMeasuredValueAvailable ? MeasuredValue.ToString($"F{Math.Max(0, DecimalPlaces)}") : "----";

        [JsonIgnore]
        public string DisplayResultValue => IsResultValueAvailable ? ReusltValue.ToString($"F{Math.Max(0, DecimalPlaces)}") : "----";

        [JsonIgnore]
        public string DisplayResultText => Result switch
        {
            MeasurementResult.Pass => "OK",
            MeasurementResult.Fail => "NG",
            _ => "--"
        };


        /// <summary>
        /// 更新最终结果值
        /// </summary>
        public void UpdateResultValue()
        {
            if (HistoricalData == null || HistoricalData.Count == 0)
            {
                ReusltValue = 0;
                IsResultValueAvailable = false;
                ChannelDescription = "没有采集到数据";
                Result = MeasurementResult.Fail;
                return;
            }
            switch (ChannelType)
            {
                case ChannelType.结果值:
                    ReusltValue = MeasuredValue;
                    break;
                case ChannelType.最大值:
                    ReusltValue = HistoricalData.Max();
                    break;
                case ChannelType.最小值:
                    ReusltValue = HistoricalData.Min();
                    break;
                case ChannelType.平均值:
                    ReusltValue = HistoricalData.Average();
                    break;
                case ChannelType.跳动值:
                case ChannelType.齿跳动值:
                    ReusltValue = HistoricalData.Max() - HistoricalData.Min();
                    break;
            }

            ReusltValue = Math.Round(ReusltValue, DecimalPlaces);
            IsResultValueAvailable = true;
            CheckResult();
        }

        public void ResetMeasurementState()
        {
            MeasuredValue = 0;
            ReusltValue = 0;
            Result = MeasurementResult.NotMeasured;
            DisplayState = MeasurementResult.Waiting;
            IsMeasuredValueAvailable = false;
            IsResultValueAvailable = false;
            ChannelDescription = string.Empty;
            HistoricalData.Clear();
        }

        partial void OnMeasurementModeChanged(MeasurementChannelMode value)
        {
            OnPropertyChanged(nameof(IsDirectMeasurementMode));
            OnPropertyChanged(nameof(IsIndirectMeasurementMode));
            OnPropertyChanged(nameof(IsVirtualMeasurementMode));
            OnPropertyChanged(nameof(DisplayPlcDeviceName));
            OnPropertyChanged(nameof(DisplayDataPointName));
            OnPropertyChanged(nameof(DisplayDataSourceAddress));
        }

        public void SetDisplayStateFromResult()
        {
            DisplayState = Result switch
            {
                MeasurementResult.Pass => MeasurementResult.Pass,
                MeasurementResult.Fail => MeasurementResult.Fail,
                _ => MeasurementResult.Waiting
            };
        }

    }


}
