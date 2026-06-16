using MeasurementSoftware.Models;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace MeasurementSoftware.Services.WriteDataPoints
{
    /// <summary>
    /// 写入点位运行时绑定服务实现。
    /// 直接在配置对象上维护可用设备引用、运行时设备/点位引用以及订阅关系。
    /// </summary>
    public sealed class WriteDataPointBindingService : IWriteDataPointBindingService
    {
        public void AttachAvailableDevices(WriteDataPointConfig config, ObservableCollection<PlcDevice>? devices)
        {
            EnsureHandlers(config);
            if (ReferenceEquals(config.AttachedAvailableDevices, devices))
            {
                SyncRuntimeDeviceFromAvailableDevices(config);
                return;
            }

            if (config.AttachedAvailableDevices != null && config.AvailableDevicesCollectionChangedHandler != null)
            {
                config.AttachedAvailableDevices.CollectionChanged -= config.AvailableDevicesCollectionChangedHandler;
            }

            config.AttachedAvailableDevices = devices;
            if (config.AttachedAvailableDevices != null && config.AvailableDevicesCollectionChangedHandler != null)
            {
                config.AttachedAvailableDevices.CollectionChanged += config.AvailableDevicesCollectionChangedHandler;
            }

            SyncRuntimeDeviceFromAvailableDevices(config);
        }

        public void DetachAvailableDevices(WriteDataPointConfig config)
        {
            EnsureHandlers(config);
            if (config.AttachedAvailableDevices != null && config.AvailableDevicesCollectionChangedHandler != null)
            {
                config.AttachedAvailableDevices.CollectionChanged -= config.AvailableDevicesCollectionChangedHandler;
                config.AttachedAvailableDevices = null;
            }

            SetRuntimeDevice(config, null, updatePersistedDeviceId: false, preservePersistedDataPointId: true);
            config.AttachedAvailableDevices = null;
        }

        public void HydrateRuntimeBindings(WriteDataPointConfig config, PlcDevice? device)
        {
            EnsureHandlers(config);
            SetRuntimeDevice(config, device, updatePersistedDeviceId: false, preservePersistedDataPointId: true);
        }

        public void BindRuntimeDevice(WriteDataPointConfig config, PlcDevice? device)
        {
            EnsureHandlers(config);
            SetRuntimeDevice(config, device, updatePersistedDeviceId: true, preservePersistedDataPointId: false);
        }

        public void BindRuntimeDataPoint(WriteDataPointConfig config, DataPoint? dataPoint)
        {
            EnsureHandlers(config);
            SetRuntimeDataPoint(config, dataPoint, updatePersistedDataPointId: true, syncDataTypeFromPoint: true);
        }

        private static void EnsureHandlers(WriteDataPointConfig config)
        {
            config.AvailableDevicesCollectionChangedHandler ??= (_, _) => SyncRuntimeDeviceFromAvailableDevices(config);
            config.RuntimeDevicePropertyChangedHandler ??= (_, e) => RuntimeDevice_PropertyChanged(config, e);
            config.RuntimeDeviceDataPointsCollectionChangedHandler ??= (_, e) => RuntimeDeviceDataPoints_CollectionChanged(config, e);
            config.RuntimeDataPointPropertyChangedHandler ??= (_, e) => RuntimeDataPointSource_PropertyChanged(config, e);
        }

        private static void RuntimeDevice_PropertyChanged(WriteDataPointConfig config, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlcDevice.IsEnabled) && config.RuntimeDevice?.IsEnabled != true)
            {
                SyncRuntimeDeviceFromAvailableDevices(config);
                return;
            }

            if (e.PropertyName is nameof(PlcDevice.DeviceName) or nameof(PlcDevice.DeviceId) or nameof(PlcDevice.IsEnabled))
            {
                RefreshAvailableDataPoints(config, preservePersistedDataPointId: true);
            }
        }

        private static void RuntimeDeviceDataPoints_CollectionChanged(WriteDataPointConfig config, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null && config.RuntimeDataPointPropertyChangedHandler != null)
            {
                foreach (DataPoint dataPoint in e.OldItems)
                {
                    dataPoint.PropertyChanged -= config.RuntimeDataPointPropertyChangedHandler;
                }
            }

            if (e.NewItems != null && config.RuntimeDataPointPropertyChangedHandler != null)
            {
                foreach (DataPoint dataPoint in e.NewItems)
                {
                    dataPoint.PropertyChanged -= config.RuntimeDataPointPropertyChangedHandler;
                    dataPoint.PropertyChanged += config.RuntimeDataPointPropertyChangedHandler;
                }
            }

            RefreshAvailableDataPoints(config, preservePersistedDataPointId: true);
        }

        private static void RuntimeDataPointSource_PropertyChanged(WriteDataPointConfig config, PropertyChangedEventArgs e)
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
                RefreshAvailableDataPoints(config, preservePersistedDataPointId: true);
            }
        }

        private static void SyncRuntimeDeviceFromAvailableDevices(WriteDataPointConfig config)
        {
            if (config.AttachedAvailableDevices == null)
            {
                HydrateRuntimeBindingsCore(config, null);
                return;
            }

            if (config.RuntimeDevice != null && config.AttachedAvailableDevices.Contains(config.RuntimeDevice))
            {
                RefreshAvailableDataPoints(config, preservePersistedDataPointId: true);
                return;
            }

            var device = config.PlcDeviceId == 0
                ? null
                : config.AttachedAvailableDevices.FirstOrDefault(d => d.DeviceId == config.PlcDeviceId);
            HydrateRuntimeBindingsCore(config, device);
        }

        private static void HydrateRuntimeBindingsCore(WriteDataPointConfig config, PlcDevice? device)
        {
            SetRuntimeDevice(config, device, updatePersistedDeviceId: false, preservePersistedDataPointId: true);
        }

        private static void SetRuntimeDevice(WriteDataPointConfig config, PlcDevice? device, bool updatePersistedDeviceId, bool preservePersistedDataPointId)
        {
            var normalizedDevice = device?.IsEnabled == true ? device : null;
            var deviceChanged = !ReferenceEquals(config.SubscribedRuntimeDevice, normalizedDevice);

            if (deviceChanged)
            {
                UnsubscribeRuntimeDevice(config);
                config.RuntimeDevice = normalizedDevice;
                config.SubscribedRuntimeDevice = normalizedDevice;
                SubscribeRuntimeDevice(config);
            }
            else if (!ReferenceEquals(config.RuntimeDevice, normalizedDevice))
            {
                config.RuntimeDevice = normalizedDevice;
            }

            if (updatePersistedDeviceId)
            {
                config.PlcDeviceId = normalizedDevice?.DeviceId ?? 0;
            }

            RefreshAvailableDataPoints(config, preservePersistedDataPointId);
        }

        private static void SetRuntimeDataPoint(WriteDataPointConfig config, DataPoint? dataPoint, bool updatePersistedDataPointId, bool syncDataTypeFromPoint)
        {
            var normalizedDataPoint = dataPoint != null && dataPoint.IsEnabled && config.AvailableDataPoints.Contains(dataPoint)
                ? dataPoint
                : null;
            var dataPointChanged = !ReferenceEquals(config.SubscribedRuntimeDataPoint, normalizedDataPoint);

            if (dataPointChanged && config.SubscribedRuntimeDataPoint != null && config.RuntimeDataPointPropertyChangedHandler != null)
            {
                config.SubscribedRuntimeDataPoint.PropertyChanged -= config.RuntimeDataPointPropertyChangedHandler;
            }

            config.RuntimeDataPoint = normalizedDataPoint;
            config.SubscribedRuntimeDataPoint = normalizedDataPoint;

            if (updatePersistedDataPointId)
            {
                config.DataPointId = normalizedDataPoint?.PointId ?? string.Empty;
            }

            if (syncDataTypeFromPoint && normalizedDataPoint != null)
            {
                config.DataType = normalizedDataPoint.DataType;
            }

            if (dataPointChanged && config.SubscribedRuntimeDataPoint != null && config.RuntimeDataPointPropertyChangedHandler != null)
            {
                config.SubscribedRuntimeDataPoint.PropertyChanged -= config.RuntimeDataPointPropertyChangedHandler;
                config.SubscribedRuntimeDataPoint.PropertyChanged += config.RuntimeDataPointPropertyChangedHandler;
            }
        }

        private static void RefreshAvailableDataPoints(WriteDataPointConfig config, bool preservePersistedDataPointId)
        {
            config.AvailableDataPoints = config.RuntimeDevice == null || !config.RuntimeDevice.IsEnabled
                ? []
                : new ObservableCollection<DataPoint>(config.RuntimeDevice.DataPoints
                    .Where(dp => dp.IsEnabled)
                    .OrderBy(dp => dp.PointName));

            DataPoint? selectedDataPoint;
            if (preservePersistedDataPointId && !string.IsNullOrWhiteSpace(config.DataPointId))
            {
                selectedDataPoint = config.AvailableDataPoints.FirstOrDefault(dp => dp.PointId == config.DataPointId)
                    ?? config.AvailableDataPoints.FirstOrDefault();
            }
            else
            {
                selectedDataPoint = config.RuntimeDataPoint != null && config.AvailableDataPoints.Contains(config.RuntimeDataPoint)
                    ? config.RuntimeDataPoint
                    : string.IsNullOrWhiteSpace(config.DataPointId)
                        ? config.AvailableDataPoints.FirstOrDefault()
                        : config.AvailableDataPoints.FirstOrDefault(dp => dp.PointId == config.DataPointId) ?? config.AvailableDataPoints.FirstOrDefault();
            }

            SetRuntimeDataPoint(config, selectedDataPoint, updatePersistedDataPointId: !preservePersistedDataPointId, syncDataTypeFromPoint: true);
        }

        private static void SubscribeRuntimeDevice(WriteDataPointConfig config)
        {
            if (config.SubscribedRuntimeDevice == null)
            {
                return;
            }

            if (config.RuntimeDevicePropertyChangedHandler != null)
            {
                config.SubscribedRuntimeDevice.PropertyChanged += config.RuntimeDevicePropertyChangedHandler;
            }

            if (config.RuntimeDeviceDataPointsCollectionChangedHandler != null)
            {
                config.SubscribedRuntimeDevice.DataPoints.CollectionChanged += config.RuntimeDeviceDataPointsCollectionChangedHandler;
            }

            if (config.RuntimeDataPointPropertyChangedHandler != null)
            {
                foreach (var dataPoint in config.SubscribedRuntimeDevice.DataPoints)
                {
                    dataPoint.PropertyChanged -= config.RuntimeDataPointPropertyChangedHandler;
                    dataPoint.PropertyChanged += config.RuntimeDataPointPropertyChangedHandler;
                }
            }
        }

        private static void UnsubscribeRuntimeDevice(WriteDataPointConfig config)
        {
            if (config.SubscribedRuntimeDevice == null)
            {
                return;
            }

            if (config.RuntimeDevicePropertyChangedHandler != null)
            {
                config.SubscribedRuntimeDevice.PropertyChanged -= config.RuntimeDevicePropertyChangedHandler;
            }

            if (config.RuntimeDeviceDataPointsCollectionChangedHandler != null)
            {
                config.SubscribedRuntimeDevice.DataPoints.CollectionChanged -= config.RuntimeDeviceDataPointsCollectionChangedHandler;
            }

            if (config.RuntimeDataPointPropertyChangedHandler != null)
            {
                foreach (var dataPoint in config.SubscribedRuntimeDevice.DataPoints)
                {
                    dataPoint.PropertyChanged -= config.RuntimeDataPointPropertyChangedHandler;
                }
            }

            config.SubscribedRuntimeDevice = null;
        }
    }
}
