using MeasurementSoftware.Models;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace MeasurementSoftware.Services.WriteDataPoints
{
    /// <summary>
    /// 写入点位运行时绑定服务实现。
    /// 执行“引用优先、Id 回填、否则不执行”的绑定策略。
    /// </summary>
    public sealed class WriteDataPointBindingService : IWriteDataPointBindingService
    {
        private readonly Dictionary<WriteDataPointConfig, BindingContext> contexts = [];

        public void AttachAvailableDevices(WriteDataPointConfig config, ObservableCollection<PlcDevice>? devices)
        {
            var context = GetOrCreateContext(config);
            if (ReferenceEquals(context.AvailableDevices, devices))
            {
                SyncRuntimeDeviceFromAvailableDevices(config, context);
                return;
            }

            if (context.AvailableDevices != null)
            {
                context.AvailableDevices.CollectionChanged -= context.AvailableDevicesCollectionChangedHandler;
            }

            context.AvailableDevices = devices;
            if (context.AvailableDevices != null)
            {
                context.AvailableDevices.CollectionChanged += context.AvailableDevicesCollectionChangedHandler;
            }

            SyncRuntimeDeviceFromAvailableDevices(config, context);
        }

        public void DetachAvailableDevices(WriteDataPointConfig config)
        {
            if (!contexts.TryGetValue(config, out var context))
            {
                return;
            }

            if (context.AvailableDevices != null)
            {
                context.AvailableDevices.CollectionChanged -= context.AvailableDevicesCollectionChangedHandler;
                context.AvailableDevices = null;
            }

            SetRuntimeDevice(config, context, null, updatePersistedDeviceId: false, preservePersistedDataPointId: true);
            contexts.Remove(config);
        }

        public void HydrateRuntimeBindings(WriteDataPointConfig config, PlcDevice? device)
        {
            var context = GetOrCreateContext(config);
            SetRuntimeDevice(config, context, device, updatePersistedDeviceId: false, preservePersistedDataPointId: true);
        }

        public void BindRuntimeDevice(WriteDataPointConfig config, PlcDevice? device)
        {
            var context = GetOrCreateContext(config);
            SetRuntimeDevice(config, context, device, updatePersistedDeviceId: true, preservePersistedDataPointId: false);
        }

        public void BindRuntimeDataPoint(WriteDataPointConfig config, DataPoint? dataPoint)
        {
            var context = GetOrCreateContext(config);
            SetRuntimeDataPoint(config, context, dataPoint, updatePersistedDataPointId: true, syncDataTypeFromPoint: true);
        }

        private BindingContext GetOrCreateContext(WriteDataPointConfig config)
        {
            if (contexts.TryGetValue(config, out var context))
            {
                return context;
            }

            context = new BindingContext();
            context.AvailableDevicesCollectionChangedHandler = (_, _) => SyncRuntimeDeviceFromAvailableDevices(config, context);
            context.RuntimeDevicePropertyChangedHandler = (_, e) => RuntimeDevice_PropertyChanged(config, context, e);
            context.RuntimeDeviceDataPointsCollectionChangedHandler = (_, e) => RuntimeDeviceDataPoints_CollectionChanged(config, context, e);
            context.RuntimeDataPointPropertyChangedHandler = (_, e) => RuntimeDataPointSource_PropertyChanged(config, context, e);
            contexts[config] = context;
            return context;
        }

        private void RuntimeDevice_PropertyChanged(WriteDataPointConfig config, BindingContext context, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlcDevice.IsEnabled) && config.RuntimeDevice?.IsEnabled != true)
            {
                HydrateRuntimeBindings(config, null);
                return;
            }

            if (e.PropertyName is nameof(PlcDevice.DeviceName) or nameof(PlcDevice.DeviceId) or nameof(PlcDevice.IsEnabled))
            {
                RefreshAvailableDataPoints(config, context, preservePersistedDataPointId: true);
            }
        }

        private void RuntimeDeviceDataPoints_CollectionChanged(WriteDataPointConfig config, BindingContext context, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (DataPoint dataPoint in e.OldItems)
                {
                    dataPoint.PropertyChanged -= context.RuntimeDataPointPropertyChangedHandler;
                }
            }

            if (e.NewItems != null)
            {
                foreach (DataPoint dataPoint in e.NewItems)
                {
                    dataPoint.PropertyChanged -= context.RuntimeDataPointPropertyChangedHandler;
                    dataPoint.PropertyChanged += context.RuntimeDataPointPropertyChangedHandler;
                }
            }

            RefreshAvailableDataPoints(config, context, preservePersistedDataPointId: true);
        }

        private void RuntimeDataPointSource_PropertyChanged(WriteDataPointConfig config, BindingContext context, PropertyChangedEventArgs e)
        {
            if (config.RuntimeDataPoint is not DataPoint dataPoint)
            {
                return;
            }

            if (e.PropertyName == nameof(DataPoint.PointId))
            {
                config.DataPointId = dataPoint.PointId;
            }

            if (e.PropertyName == nameof(DataPoint.DataType))
            {
                config.DataType = dataPoint.DataType;
            }

            if (e.PropertyName == nameof(DataPoint.CurrentValue))
            {
                config.SyncPendingWriteValueFromRuntime();
                return;
            }

            if (e.PropertyName is nameof(DataPoint.IsEnabled) or nameof(DataPoint.PointId) or nameof(DataPoint.PointName) or nameof(DataPoint.DataType))
            {
                RefreshAvailableDataPoints(config, context, preservePersistedDataPointId: true);
            }
        }

        private void SyncRuntimeDeviceFromAvailableDevices(WriteDataPointConfig config, BindingContext context)
        {
            if (context.AvailableDevices == null)
            {
                HydrateRuntimeBindings(config, null);
                return;
            }

            if (config.RuntimeDevice != null && context.AvailableDevices.Contains(config.RuntimeDevice))
            {
                RefreshAvailableDataPoints(config, context, preservePersistedDataPointId: true);
                return;
            }

            var device = config.PlcDeviceId == 0
                ? null
                : context.AvailableDevices.FirstOrDefault(d => d.DeviceId == config.PlcDeviceId);
            HydrateRuntimeBindings(config, device);
        }

        private void SetRuntimeDevice(WriteDataPointConfig config, BindingContext context, PlcDevice? device, bool updatePersistedDeviceId, bool preservePersistedDataPointId)
        {
            var normalizedDevice = device?.IsEnabled == true ? device : null;
            var deviceChanged = !ReferenceEquals(context.BoundRuntimeDevice, normalizedDevice);

            if (deviceChanged)
            {
                UnsubscribeRuntimeDevice(config, context);
                config.RuntimeDevice = normalizedDevice;
                context.BoundRuntimeDevice = normalizedDevice;
                SubscribeRuntimeDevice(config, context);
            }
            else if (!ReferenceEquals(config.RuntimeDevice, normalizedDevice))
            {
                config.RuntimeDevice = normalizedDevice;
            }

            if (updatePersistedDeviceId)
            {
                config.PlcDeviceId = normalizedDevice?.DeviceId ?? 0;
            }

            RefreshAvailableDataPoints(config, context, preservePersistedDataPointId);
        }

        private void SetRuntimeDataPoint(WriteDataPointConfig config, BindingContext context, DataPoint? dataPoint, bool updatePersistedDataPointId, bool syncDataTypeFromPoint)
        {
            var normalizedDataPoint = dataPoint != null && dataPoint.IsEnabled && config.AvailableDataPoints.Contains(dataPoint)
                ? dataPoint
                : null;
            var dataPointChanged = !ReferenceEquals(context.BoundRuntimeDataPoint, normalizedDataPoint);

            if (dataPointChanged && context.BoundRuntimeDataPoint != null)
            {
                context.BoundRuntimeDataPoint.PropertyChanged -= context.RuntimeDataPointPropertyChangedHandler;
            }

            config.RuntimeDataPoint = normalizedDataPoint;
            context.BoundRuntimeDataPoint = normalizedDataPoint;

            if (updatePersistedDataPointId)
            {
                config.DataPointId = normalizedDataPoint?.PointId ?? string.Empty;
            }

            if (syncDataTypeFromPoint && normalizedDataPoint != null)
            {
                config.DataType = normalizedDataPoint.DataType;
            }

            if (dataPointChanged && context.BoundRuntimeDataPoint != null)
            {
                context.BoundRuntimeDataPoint.PropertyChanged -= context.RuntimeDataPointPropertyChangedHandler;
                context.BoundRuntimeDataPoint.PropertyChanged += context.RuntimeDataPointPropertyChangedHandler;
            }
        }

        private void RefreshAvailableDataPoints(WriteDataPointConfig config, BindingContext context, bool preservePersistedDataPointId)
        {
            config.AvailableDataPoints = config.RuntimeDevice == null || !config.RuntimeDevice.IsEnabled
                ? []
                : new ObservableCollection<DataPoint>(config.RuntimeDevice.DataPoints
                    .Where(dp => dp.IsEnabled)
                    .OrderBy(dp => dp.PointName));

            var selectedDataPoint = config.RuntimeDataPoint != null && config.AvailableDataPoints.Contains(config.RuntimeDataPoint)
                ? config.RuntimeDataPoint
                : string.IsNullOrWhiteSpace(config.DataPointId)
                    ? config.AvailableDataPoints.FirstOrDefault()
                    : config.AvailableDataPoints.FirstOrDefault(dp => dp.PointId == config.DataPointId) ?? config.AvailableDataPoints.FirstOrDefault();

            SetRuntimeDataPoint(config, context, selectedDataPoint, updatePersistedDataPointId: !preservePersistedDataPointId, syncDataTypeFromPoint: true);
        }

        private void SubscribeRuntimeDevice(WriteDataPointConfig config, BindingContext context)
        {
            if (context.BoundRuntimeDevice == null)
            {
                return;
            }

            context.BoundRuntimeDevice.PropertyChanged += context.RuntimeDevicePropertyChangedHandler;
            context.BoundRuntimeDevice.DataPoints.CollectionChanged += context.RuntimeDeviceDataPointsCollectionChangedHandler;
            foreach (var dataPoint in context.BoundRuntimeDevice.DataPoints)
            {
                dataPoint.PropertyChanged -= context.RuntimeDataPointPropertyChangedHandler;
                dataPoint.PropertyChanged += context.RuntimeDataPointPropertyChangedHandler;
            }
        }

        private void UnsubscribeRuntimeDevice(WriteDataPointConfig config, BindingContext context)
        {
            if (context.BoundRuntimeDevice == null)
            {
                return;
            }

            context.BoundRuntimeDevice.PropertyChanged -= context.RuntimeDevicePropertyChangedHandler;
            context.BoundRuntimeDevice.DataPoints.CollectionChanged -= context.RuntimeDeviceDataPointsCollectionChangedHandler;
            foreach (var dataPoint in context.BoundRuntimeDevice.DataPoints)
            {
                dataPoint.PropertyChanged -= context.RuntimeDataPointPropertyChangedHandler;
            }

            context.BoundRuntimeDevice = null;
        }

        private sealed class BindingContext
        {
            public ObservableCollection<PlcDevice>? AvailableDevices { get; set; }

            public PlcDevice? BoundRuntimeDevice { get; set; }

            public DataPoint? BoundRuntimeDataPoint { get; set; }

            public NotifyCollectionChangedEventHandler AvailableDevicesCollectionChangedHandler { get; set; } = default!;

            public PropertyChangedEventHandler RuntimeDevicePropertyChangedHandler { get; set; } = default!;

            public NotifyCollectionChangedEventHandler RuntimeDeviceDataPointsCollectionChangedHandler { get; set; } = default!;

            public PropertyChangedEventHandler RuntimeDataPointPropertyChangedHandler { get; set; } = default!;
        }
    }
}
