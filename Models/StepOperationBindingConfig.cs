using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.ViewModels;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace MeasurementSoftware.Models
{
    /// <summary>
    /// 工步操作绑定配置。
    /// 用于定义开始采集、停止采集、上一步、下一步对应的设备点位和触发方式。
    /// </summary>
    public partial class StepOperationBindingConfig : ObservableViewModel
    {
        /// <summary>
        /// 工步操作类型。
        /// </summary>
        [ObservableProperty]
        private StepOperationType operationType;

        /// <summary>
        /// 是否启用当前工步操作绑定。
        /// </summary>
        [ObservableProperty]
        private bool isEnabled = true;

        /// <summary>
        /// 绑定的 PLC 设备 ID。
        /// </summary>
        [ObservableProperty]
        private long plcDeviceId;

        /// <summary>
        /// 绑定的数据点 ID。
        /// </summary>
        [ObservableProperty]
        private string dataPointId = string.Empty;

        /// <summary>
        /// 触发方式。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RequiresTriggerValue))]
        private StepOperationTriggerMode triggerMode = StepOperationTriggerMode.RisingEdge;

        /// <summary>
        /// 触发值。
        /// 当触发方式为“值等于”时生效。
        /// </summary>
        [ObservableProperty]
        private string triggerValue = "true";

        /// <summary>
        /// 当前设备下可选的数据点列表。
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<DataPoint> availableDataPoints = [];

        private PlcDevice? runtimeDevice;

        [JsonIgnore]
        public PlcDevice? RuntimeDevice
        {
            get => runtimeDevice;
            set
            {
                if (ReferenceEquals(runtimeDevice, value))
                {
                    return;
                }

                runtimeDevice = value;
                PlcDeviceId = value?.DeviceId ?? 0;
                RefreshAvailableDataPoints();

                if (runtimeDataPoint == null || !AvailableDataPoints.Contains(runtimeDataPoint))
                {
                    RuntimeDataPoint = AvailableDataPoints.FirstOrDefault(dp => dp.PointId == DataPointId)
                        ?? AvailableDataPoints.FirstOrDefault();
                }

                OnPropertyChanged(nameof(RuntimeDevice));
            }
        }

        private DataPoint? runtimeDataPoint;

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

                runtimeDataPoint = value;
                DataPointId = value?.PointId ?? string.Empty;
                OnPropertyChanged(nameof(RuntimeDataPoint));
            }
        }

        /// <summary>
        /// 按已保存的设备/点位标识回填运行时绑定。
        /// 仅刷新运行时引用，不修改持久化的设备与点位 ID。
        /// </summary>
        public void HydrateRuntimeBindings(PlcDevice? device)
        {
            runtimeDevice = device?.IsEnabled == true ? device : null;
            AvailableDataPoints = runtimeDevice == null || !runtimeDevice.IsEnabled
                ? []
                : new ObservableCollection<DataPoint>(runtimeDevice.DataPoints
                    .Where(dp => dp.IsEnabled)
                    .OrderBy(dp => dp.PointName));

            runtimeDataPoint = AvailableDataPoints.FirstOrDefault(dp => dp.PointId == DataPointId);

            OnPropertyChanged(nameof(RuntimeDevice));
            OnPropertyChanged(nameof(RuntimeDataPoint));
        }

        /// <summary>
        /// 按当前点位 ID 同步运行时点位引用。
        /// </summary>
        public void SyncRuntimeDataPointReference()
        {
            runtimeDataPoint = AvailableDataPoints.FirstOrDefault(dp => dp.PointId == DataPointId);
            OnPropertyChanged(nameof(RuntimeDataPoint));
        }

        [JsonIgnore]
        public bool RequiresTriggerValue => TriggerMode == StepOperationTriggerMode.ValueEquals;

        /// <summary>
        /// 上一次观测到的点位值。
        /// 仅运行时使用，不参与序列化。
        /// </summary>
        [JsonIgnore]
        public object? LastObservedValue { get; set; }

        /// <summary>
        /// 是否已经记录过上一拍观测值。
        /// 仅运行时使用，不参与序列化。
        /// </summary>
        [JsonIgnore]
        public bool HasObservedValue { get; set; }

        /// <summary>
        /// 绑定运行时设备实例。
        /// </summary>
        public void BindDevice(PlcDevice? device)
        {
            RuntimeDevice = device?.IsEnabled == true ? device : null;
        }

        /// <summary>
        /// 绑定运行时数据点实例。
        /// </summary>
        public void BindDataPoint(DataPoint? dataPoint)
        {
            RuntimeDataPoint = dataPoint?.IsEnabled == true ? dataPoint : null;
        }

        /// <summary>
        /// 根据当前设备刷新可选点位。
        /// 如果设备未启用，则点位列表会自动清空。
        /// </summary>
        public void RefreshAvailableDataPoints()
        {
            AvailableDataPoints = RuntimeDevice == null || !RuntimeDevice.IsEnabled
                ? []
                : new ObservableCollection<DataPoint>(RuntimeDevice.DataPoints
                    .Where(dp => dp.IsEnabled)
                    .OrderBy(dp => dp.PointName));

            RuntimeDataPoint = AvailableDataPoints.FirstOrDefault(dp => dp.PointId == DataPointId);
        }

        /// <summary>
        /// 重置运行时观测值。
        /// </summary>
        public void ResetObservedValue()
        {
            HasObservedValue = false;
            LastObservedValue = null;
        }
    }
}
