using CommunityToolkit.Mvvm.ComponentModel;
using MultiProtocol.Model;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace MeasurementSoftware.Models
{
    /// <summary>
    /// 间接测量的数据源绑定项。
    /// 每个绑定项对应一个变量名与一个设备点位。
    /// </summary>
    public partial class MeasurementChannelSourceBinding : ObservableObject
    {
        /// <summary>
        /// 公式变量名。
        /// 例如 X1、DIA、A 等。
        /// </summary>
        [ObservableProperty]
        private string sourceKey = string.Empty;

        /// <summary>
        /// 关联的 PLC 设备 ID。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PlcDeviceName))]
        private long plcDeviceId;

        /// <summary>
        /// 关联的数据点 ID。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DataPointName))]
        private string dataPointId = string.Empty;

        /// <summary>
        /// 数据源地址。
        /// </summary>
        [ObservableProperty]
        private string dataSourceAddress = string.Empty;

        /// <summary>
        /// 可用的数据点列表。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DataPointName))]
        private ObservableCollection<DataPoint> availableDataPoints = new();

        private PlcDevice? runtimeDevice;
        private PropertyChangedEventHandler? runtimeDevicePropertyChangedHandler;
        private NotifyCollectionChangedEventHandler? runtimeDeviceDataPointsCollectionChangedHandler;

        /// <summary>
        /// 运行时绑定的 PLC 设备。
        /// </summary>
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
                }

                PlcDeviceId = value?.DeviceId ?? 0;
                RefreshAvailableDataPoints();

                if (runtimeDataPoint == null || !AvailableDataPoints.Contains(runtimeDataPoint))
                {
                    RuntimeDataPoint = GetPreferredDataPoint();
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
            }
        }

        private DataPoint? runtimeDataPoint;
        private PropertyChangedEventHandler? runtimeDataPointPropertyChangedHandler;

        /// <summary>
        /// 运行时绑定的数据点。
        /// </summary>
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
            }
        }

        /// <summary>
        /// PLC 设备名称。
        /// </summary>
        [JsonIgnore]
        public string PlcDeviceName
        {
            get
            {
                if (PlcDeviceId == 0)
                {
                    return string.Empty;
                }

                return RuntimeDevice?.DeviceName ?? PlcDeviceId.ToString();
            }
        }

        /// <summary>
        /// 数据点名称。
        /// </summary>
        [JsonIgnore]
        public string DataPointName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(DataPointId))
                {
                    return string.Empty;
                }

                if (RuntimeDataPoint != null)
                {
                    return RuntimeDataPoint.PointName;
                }

                var point = AvailableDataPoints.FirstOrDefault(p => p.PointId == DataPointId);
                return point?.PointName ?? DataPointId;
            }
        }

        /// <summary>
        /// 按已保存的设备与点位标识回填运行时绑定。
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

            runtimeDataPoint = GetPreferredDataPoint();

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
        }

        /// <summary>
        /// 清空运行时绑定。
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
            OnPropertyChanged(nameof(PlcDeviceName));
            OnPropertyChanged(nameof(DataPointName));
        }

        /// <summary>
        /// 刷新可用点位列表。
        /// </summary>
        public void RefreshAvailableDataPoints()
        {
            var currentRuntimeDataPoint = runtimeDataPoint;
            UnsubscribeFromAvailableDataPoints(AvailableDataPoints);
            AvailableDataPoints = RuntimeDevice == null
                ? []
                : new ObservableCollection<DataPoint>(RuntimeDevice.DataPoints
                    .Where(dp => dp.IsEnabled)
                    .OrderBy(dp => int.TryParse(dp.PointId, out var id) ? id : int.MaxValue));
            SubscribeToAvailableDataPoints(AvailableDataPoints);

            if (currentRuntimeDataPoint != null && AvailableDataPoints.Contains(currentRuntimeDataPoint))
            {
                RuntimeDataPoint = currentRuntimeDataPoint;
            }
            else
            {
                RuntimeDataPoint = GetPreferredDataPoint();
            }

            if (RuntimeDataPoint != null)
            {
                DataSourceAddress = RuntimeDataPoint.Address;
            }

            OnPropertyChanged(nameof(DataPointName));
        }

        /// <summary>
        /// 优先按已保存点位回填；如果当前没有已保存点位，则默认选中第一个可用点位。
        /// </summary>
        private DataPoint? GetPreferredDataPoint()
        {
            if (AvailableDataPoints.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(DataPointId))
            {
                return AvailableDataPoints.FirstOrDefault(dp => dp.PointId == DataPointId) ?? AvailableDataPoints.FirstOrDefault();
            }

            return AvailableDataPoints.FirstOrDefault();
        }

        /// <summary>
        /// 创建当前绑定项的浅拷贝，用于编辑时隔离原始配置。
        /// </summary>
        public MeasurementChannelSourceBinding Clone()
        {
            return new MeasurementChannelSourceBinding
            {
                SourceKey = SourceKey,
                PlcDeviceId = PlcDeviceId,
                DataPointId = DataPointId,
                DataSourceAddress = DataSourceAddress
            };
        }

        private void RuntimeDevice_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlcDevice.IsEnabled) && runtimeDevice?.IsEnabled != true)
            {
                ClearRuntimeBindings();
                return;
            }

            if (e.PropertyName is nameof(PlcDevice.DeviceName) or nameof(PlcDevice.IsEnabled) or nameof(PlcDevice.DeviceId))
            {
                RefreshAvailableDataPoints();
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
