using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.ViewModels;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
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

        private ObservableCollection<PlcDevice>? availableDevices;
        private PlcDevice? runtimeDevice;
        private DataPoint? runtimeDataPoint;

        /// <summary>
        /// 当前绑定的运行时设备实例。
        /// 仅运行时使用，不参与序列化。
        /// </summary>
        [JsonIgnore]
        public PlcDevice? RuntimeDevice
        {
            get => runtimeDevice;
            set
            {
                SetRuntimeDevice(value, updatePersistedDeviceId: true, preservePersistedDataPointId: false);
            }
        }

        /// <summary>
        /// 当前绑定的运行时点位实例。
        /// 仅运行时使用，不参与序列化。
        /// </summary>
        [JsonIgnore]
        public DataPoint? RuntimeDataPoint
        {
            get => runtimeDataPoint;
            set
            {
                SetRuntimeDataPoint(value, updatePersistedDataPointId: true);
            }
        }

        /// <summary>
        /// 按已保存的设备/点位标识回填运行时绑定。
        /// 仅刷新运行时引用，不修改持久化的设备与点位 ID。
        /// </summary>
        public void HydrateRuntimeBindings(PlcDevice? device)
        {
            SetRuntimeDevice(device, updatePersistedDeviceId: false, preservePersistedDataPointId: true);
        }

        /// <summary>
        /// 按当前点位 ID 同步运行时点位引用。
        /// </summary>
        public void SyncRuntimeDataPointReference()
        {
            SetRuntimeDataPoint(AvailableDataPoints.FirstOrDefault(dp => dp.PointId == DataPointId), updatePersistedDataPointId: false);
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
            SetRuntimeDevice(device, updatePersistedDeviceId: true, preservePersistedDataPointId: false);
        }

        /// <summary>
        /// 绑定运行时数据点实例。
        /// </summary>
        public void BindDataPoint(DataPoint? dataPoint)
        {
            SetRuntimeDataPoint(dataPoint, updatePersistedDataPointId: true);
        }

        /// <summary>
        /// 绑定当前可选设备集合。
        /// 模型内部直接监听设备集合变化，并按已保存的设备 ID 自动恢复运行时引用。
        /// </summary>
        public void AttachAvailableDevices(ObservableCollection<PlcDevice>? devices)
        {
            if (ReferenceEquals(availableDevices, devices))
            {
                SyncRuntimeDeviceFromAvailableDevices();
                return;
            }

            availableDevices?.CollectionChanged -= AvailableDevices_CollectionChanged;

            availableDevices = devices;

            if (availableDevices != null)
            {
                availableDevices.CollectionChanged += AvailableDevices_CollectionChanged;
            }

            SyncRuntimeDeviceFromAvailableDevices();
        }

        /// <summary>
        /// 解除可选设备集合监听。
        /// </summary>
        public void DetachAvailableDevices()
        {
            if (availableDevices != null)
            {
                availableDevices.CollectionChanged -= AvailableDevices_CollectionChanged;
                availableDevices = null;
            }

            HydrateRuntimeBindings(null);
        }

        /// <summary>
        /// 根据当前设备刷新可选点位。
        /// 如果设备未启用，则点位列表会自动清空。
        /// </summary>
        public void RefreshAvailableDataPoints()
        {
            RefreshAvailableDataPointsCore(preservePersistedDataPointId: false);
        }

        /// <summary>
        /// 重置运行时观测值。
        /// </summary>
        public void ResetObservedValue()
        {
            HasObservedValue = false;
            LastObservedValue = null;
        }

        /// <summary>
        /// 可选设备集合变化时，按当前持久化设备 ID 重新同步运行时设备引用。
        /// </summary>
        private void AvailableDevices_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            SyncRuntimeDeviceFromAvailableDevices();
        }

        /// <summary>
        /// 监听当前运行时设备的关键属性变化，联动刷新点位集合与运行时引用。
        /// </summary>
        private void RuntimeDevice_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlcDevice.IsEnabled) && runtimeDevice?.IsEnabled != true)
            {
                HydrateRuntimeBindings(null);
                return;
            }

            if (e.PropertyName == nameof(PlcDevice.DeviceId) || e.PropertyName == nameof(PlcDevice.IsEnabled))
            {
                RefreshAvailableDataPointsCore(preservePersistedDataPointId: true);
                OnPropertyChanged(nameof(RuntimeDevice));
            }
        }

        /// <summary>
        /// 监听当前运行时设备点位集合的增删变化。
        /// </summary>
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

            RefreshAvailableDataPointsCore(preservePersistedDataPointId: true);
        }

        /// <summary>
        /// 监听当前运行时点位源对象的关键属性变化。
        /// </summary>
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

            if (e.PropertyName is nameof(DataPoint.IsEnabled) or nameof(DataPoint.PointId) or nameof(DataPoint.PointName))
            {
                RefreshAvailableDataPointsCore(preservePersistedDataPointId: true);
            }
        }

        /// <summary>
        /// 根据当前可选设备集合和已保存设备 ID，同步运行时设备引用。
        /// </summary>
        private void SyncRuntimeDeviceFromAvailableDevices()
        {
            if (availableDevices == null)
            {
                HydrateRuntimeBindings(null);
                return;
            }

            if (runtimeDevice != null && availableDevices.Contains(runtimeDevice))
            {
                RefreshAvailableDataPointsCore(preservePersistedDataPointId: true);
                return;
            }

            var device = PlcDeviceId == 0
                ? null
                : availableDevices.FirstOrDefault(d => d.DeviceId == PlcDeviceId);

            HydrateRuntimeBindings(device);
        }

        /// <summary>
        /// 设置运行时设备，并按需同步持久化设备 ID 与点位状态。
        /// </summary>
        private void SetRuntimeDevice(PlcDevice? device, bool updatePersistedDeviceId, bool preservePersistedDataPointId)
        {
            var normalizedDevice = device?.IsEnabled == true ? device : null;
            var deviceChanged = !ReferenceEquals(runtimeDevice, normalizedDevice);

            if (deviceChanged)
            {
                UnsubscribeRuntimeDevice();
                runtimeDevice = normalizedDevice;
                SubscribeRuntimeDevice();
            }

            if (updatePersistedDeviceId)
            {
                PlcDeviceId = normalizedDevice?.DeviceId ?? 0;
            }

            RefreshAvailableDataPointsCore(preservePersistedDataPointId);

            if (deviceChanged)
            {
                OnPropertyChanged(nameof(RuntimeDevice));
            }
        }

        /// <summary>
        /// 设置运行时点位，并按需同步持久化点位 ID。
        /// </summary>
        private void SetRuntimeDataPoint(DataPoint? dataPoint, bool updatePersistedDataPointId)
        {
            var normalizedDataPoint = dataPoint != null && dataPoint.IsEnabled && AvailableDataPoints.Contains(dataPoint)
                ? dataPoint
                : null;

            var dataPointChanged = !ReferenceEquals(runtimeDataPoint, normalizedDataPoint);
            runtimeDataPoint = normalizedDataPoint;

            if (updatePersistedDataPointId)
            {
                DataPointId = normalizedDataPoint?.PointId ?? string.Empty;
            }

            if (dataPointChanged)
            {
                OnPropertyChanged(nameof(RuntimeDataPoint));
            }
        }

        /// <summary>
        /// 按当前运行时设备刷新可选点位集合，并自动恢复或回退到首个可用点位。
        /// </summary>
        private void RefreshAvailableDataPointsCore(bool preservePersistedDataPointId)
        {
            AvailableDataPoints = runtimeDevice == null || !runtimeDevice.IsEnabled
                ? []
                : new ObservableCollection<DataPoint>(runtimeDevice.DataPoints
                    .Where(dp => dp.IsEnabled)
                    .OrderBy(dp => dp.PointName));

            var selectedDataPoint = AvailableDataPoints.FirstOrDefault(dp => dp.PointId == DataPointId)
                ?? AvailableDataPoints.FirstOrDefault();
            SetRuntimeDataPoint(selectedDataPoint, updatePersistedDataPointId: !preservePersistedDataPointId);
        }

        /// <summary>
        /// 为当前运行时设备及其点位集合挂接监听。
        /// </summary>
        private void SubscribeRuntimeDevice()
        {
            if (runtimeDevice == null)
            {
                return;
            }

            runtimeDevice.PropertyChanged -= RuntimeDevice_PropertyChanged;
            runtimeDevice.PropertyChanged += RuntimeDevice_PropertyChanged;
            runtimeDevice.DataPoints.CollectionChanged -= RuntimeDeviceDataPoints_CollectionChanged;
            runtimeDevice.DataPoints.CollectionChanged += RuntimeDeviceDataPoints_CollectionChanged;

            foreach (var dataPoint in runtimeDevice.DataPoints)
            {
                dataPoint.PropertyChanged -= RuntimeDataPointSource_PropertyChanged;
                dataPoint.PropertyChanged += RuntimeDataPointSource_PropertyChanged;
            }
        }

        /// <summary>
        /// 移除当前运行时设备及其点位集合上的监听。
        /// </summary>
        private void UnsubscribeRuntimeDevice()
        {
            if (runtimeDevice == null)
            {
                return;
            }

            runtimeDevice.PropertyChanged -= RuntimeDevice_PropertyChanged;
            runtimeDevice.DataPoints.CollectionChanged -= RuntimeDeviceDataPoints_CollectionChanged;

            foreach (var dataPoint in runtimeDevice.DataPoints)
            {
                dataPoint.PropertyChanged -= RuntimeDataPointSource_PropertyChanged;
            }
        }
    }
}
