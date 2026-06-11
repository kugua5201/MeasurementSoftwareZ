using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.ViewModels;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;

namespace MeasurementSoftware.Models
{
    /// <summary>
    /// 通用 PLC 触发绑定配置。
    /// 用于置零、自动校准等需要“设备 + 点位 + 触发方式”的场景。
    /// </summary>
    public partial class PlcTriggerBindingConfig : ObservableViewModel
    {
        private long plcDeviceId;

        public long PlcDeviceId
        {
            get => plcDeviceId;
            set => SetProperty(ref plcDeviceId, value);
        }

        private string dataPointId = string.Empty;

        public string DataPointId
        {
            get => dataPointId;
            set => SetProperty(ref dataPointId, value);
        }

        private StepOperationTriggerMode triggerMode = StepOperationTriggerMode.RisingEdge;

        public StepOperationTriggerMode TriggerMode
        {
            get => triggerMode;
            set => SetProperty(ref triggerMode, value, () => OnPropertyChanged(nameof(RequiresTriggerValue)));
        }

        private string triggerValue = "true";

        public string TriggerValue
        {
            get => triggerValue;
            set => SetProperty(ref triggerValue, value);
        }

        private ObservableCollection<DataPoint> availableDataPoints = [];

        public ObservableCollection<DataPoint> AvailableDataPoints
        {
            get => availableDataPoints;
            set => SetProperty(ref availableDataPoints, value ?? []);
        }

        private ObservableCollection<PlcDevice>? availableDevices;
        private PlcDevice? runtimeDevice;
        private DataPoint? runtimeDataPoint;

        [JsonIgnore]
        public PlcDevice? RuntimeDevice
        {
            get => runtimeDevice;
            set => SetRuntimeDevice(value, true, false);
        }

        [JsonIgnore]
        public DataPoint? RuntimeDataPoint
        {
            get => runtimeDataPoint;
            set => SetRuntimeDataPoint(value, true);
        }

        [JsonIgnore]
        public bool RequiresTriggerValue => TriggerMode == StepOperationTriggerMode.ValueEquals;

        [JsonIgnore]
        public object? LastObservedValue { get; set; }

        [JsonIgnore]
        public bool HasObservedValue { get; set; }

        public PlcTriggerBindingConfig Clone()
        {
            return new PlcTriggerBindingConfig
            {
                PlcDeviceId = PlcDeviceId,
                DataPointId = DataPointId,
                TriggerMode = TriggerMode,
                TriggerValue = TriggerValue
            };
        }

        public void HydrateRuntimeBindings(PlcDevice? device)
        {
            SetRuntimeDevice(device, false, true);
        }

        public void AttachAvailableDevices(ObservableCollection<PlcDevice>? devices)
        {
            if (ReferenceEquals(availableDevices, devices))
            {
                SyncRuntimeDeviceFromAvailableDevices();
                return;
            }

            if (availableDevices != null)
            {
                availableDevices.CollectionChanged -= AvailableDevices_CollectionChanged;
            }

            availableDevices = devices;
            if (availableDevices != null)
            {
                availableDevices.CollectionChanged += AvailableDevices_CollectionChanged;
            }

            SyncRuntimeDeviceFromAvailableDevices();
        }

        public void DetachAvailableDevices()
        {
            if (availableDevices != null)
            {
                availableDevices.CollectionChanged -= AvailableDevices_CollectionChanged;
                availableDevices = null;
            }

            HydrateRuntimeBindings(null);
        }

        public void ResetObservedValue()
        {
            HasObservedValue = false;
            LastObservedValue = null;
        }

        public void RefreshAvailableDataPoints()
        {
            RefreshAvailableDataPointsCore(false);
        }

        private void AvailableDevices_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            SyncRuntimeDeviceFromAvailableDevices();
        }

        private void SyncRuntimeDeviceFromAvailableDevices()
        {
            if (availableDevices == null)
            {
                HydrateRuntimeBindings(null);
                return;
            }

            var device = availableDevices.FirstOrDefault(item => item.DeviceId == PlcDeviceId && item.IsEnabled);
            SetRuntimeDevice(device, false, true);
        }

        private void SetRuntimeDevice(PlcDevice? device, bool updatePersistedDeviceId, bool preservePersistedDataPointId)
        {
            if (ReferenceEquals(runtimeDevice, device))
            {
                RefreshAvailableDataPointsCore(preservePersistedDataPointId);
                return;
            }

            if (runtimeDevice != null)
            {
                runtimeDevice.PropertyChanged -= RuntimeDevice_PropertyChanged;
                runtimeDevice.DataPoints.CollectionChanged -= RuntimeDeviceDataPoints_CollectionChanged;
            }

            runtimeDevice = device;
            if (runtimeDevice != null)
            {
                runtimeDevice.PropertyChanged += RuntimeDevice_PropertyChanged;
                runtimeDevice.DataPoints.CollectionChanged += RuntimeDeviceDataPoints_CollectionChanged;
            }

            if (updatePersistedDeviceId)
            {
                PlcDeviceId = runtimeDevice?.DeviceId ?? 0;
            }

            RefreshAvailableDataPointsCore(preservePersistedDataPointId);
            OnPropertyChanged(nameof(RuntimeDevice));
        }

        private void SetRuntimeDataPoint(DataPoint? dataPoint, bool updatePersistedDataPointId)
        {
            if (ReferenceEquals(runtimeDataPoint, dataPoint))
            {
                return;
            }

            if (runtimeDataPoint != null)
            {
                runtimeDataPoint.PropertyChanged -= RuntimeDataPoint_PropertyChanged;
            }

            runtimeDataPoint = dataPoint;
            if (runtimeDataPoint != null)
            {
                runtimeDataPoint.PropertyChanged += RuntimeDataPoint_PropertyChanged;
            }

            if (updatePersistedDataPointId)
            {
                DataPointId = runtimeDataPoint?.PointId ?? string.Empty;
            }

            OnPropertyChanged(nameof(RuntimeDataPoint));
        }

        private void RefreshAvailableDataPointsCore(bool preservePersistedDataPointId)
        {
            UnsubscribeFromAvailableDataPoints(AvailableDataPoints);
            AvailableDataPoints = runtimeDevice == null
                ? []
                : new ObservableCollection<DataPoint>(runtimeDevice.DataPoints.Where(dp => dp.IsEnabled));
            SubscribeToAvailableDataPoints(AvailableDataPoints);

            var point = AvailableDataPoints.FirstOrDefault(dp => dp.PointId == DataPointId);
            if (point == null && !preservePersistedDataPointId)
            {
                point = AvailableDataPoints.FirstOrDefault();
            }

            SetRuntimeDataPoint(point, !preservePersistedDataPointId);
        }

        private void RuntimeDevice_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlcDevice.IsEnabled) && runtimeDevice?.IsEnabled != true)
            {
                HydrateRuntimeBindings(null);
                return;
            }

            if (e.PropertyName is nameof(PlcDevice.DeviceId) or nameof(PlcDevice.IsEnabled))
            {
                RefreshAvailableDataPointsCore(true);
                OnPropertyChanged(nameof(RuntimeDevice));
            }
        }

        private void RuntimeDeviceDataPoints_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (DataPoint item in e.OldItems)
                {
                    item.PropertyChanged -= RuntimeDataPointSource_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (DataPoint item in e.NewItems)
                {
                    item.PropertyChanged -= RuntimeDataPointSource_PropertyChanged;
                    item.PropertyChanged += RuntimeDataPointSource_PropertyChanged;
                }
            }

            RefreshAvailableDataPointsCore(true);
        }

        private void RuntimeDataPointSource_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (ReferenceEquals(runtimeDataPoint, sender) && e.PropertyName == nameof(DataPoint.PointId) && sender is DataPoint point)
            {
                DataPointId = point.PointId;
            }

            if (e.PropertyName is nameof(DataPoint.IsEnabled) or nameof(DataPoint.PointId) or nameof(DataPoint.PointName))
            {
                RefreshAvailableDataPointsCore(true);
            }
        }

        private void RuntimeDataPoint_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DataPoint.PointId) && sender is DataPoint point)
            {
                DataPointId = point.PointId;
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
    }
}
