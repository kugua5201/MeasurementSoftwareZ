using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.ViewModels;
using MultiProtocol.Model;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace MeasurementSoftware.Models
{
    /// <summary>
    /// 总测量结果输出配置。
    /// 用于在整次测量完成后，将总 OK/NG 结果写入指定 PLC 点位。
    /// </summary>
    public partial class OverallMeasurementResultOutputConfig : ObservableViewModel
    {
        /// <summary>
        /// 是否启用总测量结果输出。
        /// </summary>
        [ObservableProperty]
        private bool isEnabled;

        /// <summary>
        /// 输出目标 PLC 设备 ID。
        /// </summary>
        [ObservableProperty]
        private long plcDeviceId;

        /// <summary>
        /// 输出目标点位 ID。
        /// </summary>
        [ObservableProperty]
        private string dataPointId = string.Empty;

        /// <summary>
        /// 输出地址。
        /// </summary>
        [ObservableProperty]
        private string outputAddress = string.Empty;

        /// <summary>
        /// 总结果为 OK 时写入的值。
        /// Bool 点位会自动使用 True。
        /// </summary>
        [ObservableProperty]
        private string okValue = bool.TrueString;

        /// <summary>
        /// 总结果为 NG 时写入的值。
        /// Bool 点位会自动使用 False。
        /// </summary>
        [ObservableProperty]
        private string ngValue = bool.FalseString;

        /// <summary>
        /// 当前设备下可选的输出点位列表。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DataPointName))]
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
            set => SetRuntimeDevice(value, updatePersistedDeviceId: true, preservePersistedDataPointId: false);
        }

        /// <summary>
        /// 当前绑定的运行时点位实例。
        /// 仅运行时使用，不参与序列化。
        /// </summary>
        [JsonIgnore]
        public DataPoint? RuntimeDataPoint
        {
            get => runtimeDataPoint;
            set => SetRuntimeDataPoint(value, updatePersistedDataPointId: true);
        }

        /// <summary>
        /// 当前输出点位是否为 Bool 类型。
        /// </summary>
        [JsonIgnore]
        public bool IsBoolDataPoint => RuntimeDataPoint?.DataType == FieldType.Bool;

        /// <summary>
        /// 输出设备显示名称。
        /// </summary>
        [JsonIgnore]
        public string PlcDeviceName => RuntimeDevice?.DeviceName ?? string.Empty;

        /// <summary>
        /// 输出点位显示名称。
        /// </summary>
        [JsonIgnore]
        public string DataPointName => RuntimeDataPoint?.PointName ?? string.Empty;

        /// <summary>
        /// 按已保存的设备与点位标识回填运行时绑定。
        /// </summary>
        public void HydrateRuntimeBindings(PlcDevice? device)
        {
            SetRuntimeDevice(device, updatePersistedDeviceId: false, preservePersistedDataPointId: true);
        }

        /// <summary>
        /// 绑定当前可选设备集合。
        /// 模型内部直接监听设备和点位变化，保持界面与运行时配置实时联动。
        /// </summary>
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

        private void AvailableDevices_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            SyncRuntimeDeviceFromAvailableDevices();
        }

        private void RuntimeDevice_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlcDevice.IsEnabled) && runtimeDevice?.IsEnabled != true)
            {
                HydrateRuntimeBindings(null);
                return;
            }

            if (e.PropertyName is nameof(PlcDevice.DeviceName) or nameof(PlcDevice.IsEnabled) or nameof(PlcDevice.DeviceId))
            {
                RefreshAvailableDataPointsCore(preservePersistedDataPointId: true);
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

            RefreshAvailableDataPointsCore(preservePersistedDataPointId: true);
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
                OutputAddress = dataPoint.Address;
            }

            if (e.PropertyName is nameof(DataPoint.IsEnabled) or nameof(DataPoint.PointId) or nameof(DataPoint.PointName) or nameof(DataPoint.Address))
            {
                RefreshAvailableDataPointsCore(preservePersistedDataPointId: true);
            }
        }

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
                OnPropertyChanged(nameof(PlcDeviceName));
            }
        }

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

            OutputAddress = normalizedDataPoint?.Address ?? string.Empty;
            if (normalizedDataPoint?.DataType == FieldType.Bool)
            {
                OkValue = bool.TrueString;
                NgValue = bool.FalseString;
            }

            if (dataPointChanged)
            {
                OnPropertyChanged(nameof(RuntimeDataPoint));
                OnPropertyChanged(nameof(DataPointName));
                OnPropertyChanged(nameof(IsBoolDataPoint));
            }
        }

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

        private void SubscribeRuntimeDevice()
        {
            if (runtimeDevice == null)
            {
                return;
            }

            runtimeDevice.PropertyChanged += RuntimeDevice_PropertyChanged;
            runtimeDevice.DataPoints.CollectionChanged += RuntimeDeviceDataPoints_CollectionChanged;
            foreach (var dataPoint in runtimeDevice.DataPoints)
            {
                dataPoint.PropertyChanged -= RuntimeDataPointSource_PropertyChanged;
                dataPoint.PropertyChanged += RuntimeDataPointSource_PropertyChanged;
            }
        }

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
