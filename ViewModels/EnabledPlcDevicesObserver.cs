using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Config;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace MeasurementSoftware.ViewModels
{
    /// <summary>
    /// 统一维护“当前启用设备集合”的监听器。
    /// 负责监听设备集合替换、设备增删以及设备启用状态变化，并同步输出启用设备列表。
    /// </summary>
    public sealed class EnabledPlcDevicesObserver
    {
        private readonly IDeviceConfigService _deviceConfigService;
        private ObservableCollection<PlcDevice>? _observedDevices;
        private readonly ReadOnlyObservableCollection<PlcDevice> _enabledDevicesView;

        /// <summary>
        /// 当前启用设备的可观察集合。
        /// 供需要继续向下透传可观察集合的模型使用。
        /// </summary>
        public ObservableCollection<PlcDevice> EnabledDevices { get; } = [];

        /// <summary>
        /// 当前启用设备的只读视图。
        /// 供界面层直接绑定，避免外部误修改集合。
        /// </summary>
        public ReadOnlyObservableCollection<PlcDevice> EnabledDevicesView => _enabledDevicesView;

        /// <summary>
        /// 当启用设备集合发生变化时触发。
        /// </summary>
        public event EventHandler? Changed;

        /// <summary>
        /// 创建启用设备集合观察器。
        /// </summary>
        public EnabledPlcDevicesObserver(IDeviceConfigService deviceConfigService)
        {
            _deviceConfigService = deviceConfigService;
            _enabledDevicesView = new ReadOnlyObservableCollection<PlcDevice>(EnabledDevices);

            if (_deviceConfigService is INotifyPropertyChanged notifyPropertyChanged)
            {
                PropertyChangedEventManager.AddHandler(notifyPropertyChanged, DeviceConfigService_PropertyChanged, nameof(IDeviceConfigService.Devices));
            }

            Rebind();
        }

        /// <summary>
        /// 重新绑定设备源集合并刷新启用设备列表。
        /// 当配方切换或设备集合引用被替换时调用。
        /// </summary>
        public void Rebind()
        {
            if (!ReferenceEquals(_observedDevices, _deviceConfigService.Devices))
            {
                if (_observedDevices != null)
                {
                    CollectionChangedEventManager.RemoveHandler(_observedDevices, Devices_CollectionChanged);
                    foreach (var device in _observedDevices)
                    {
                        PropertyChangedEventManager.RemoveHandler(device, Device_PropertyChanged, string.Empty);
                    }
                }

                _observedDevices = _deviceConfigService.Devices;
                CollectionChangedEventManager.AddHandler(_observedDevices, Devices_CollectionChanged);
                foreach (var device in _observedDevices)
                {
                    PropertyChangedEventManager.AddHandler(device, Device_PropertyChanged, string.Empty);
                }
            }

            Refresh();
        }

        private void DeviceConfigService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            Rebind();
        }

        private void Devices_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (PlcDevice device in e.OldItems)
                {
                    PropertyChangedEventManager.RemoveHandler(device, Device_PropertyChanged, string.Empty);
                }
            }

            if (e.NewItems != null)
            {
                foreach (PlcDevice device in e.NewItems)
                {
                    PropertyChangedEventManager.AddHandler(device, Device_PropertyChanged, string.Empty);
                }
            }

            Refresh();
        }

        private void Device_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is not nameof(PlcDevice.IsEnabled) and not nameof(PlcDevice.DeviceId))
            {
                return;
            }

            Refresh();
        }

        private void Refresh()
        {
            var enabledDevices = _deviceConfigService.Devices.Where(device => device.IsEnabled).ToList();

            var removedDevices = EnabledDevices
                .Where(existing => enabledDevices.All(device => device.DeviceId != existing.DeviceId))
                .ToList();

            foreach (var removedDevice in removedDevices)
            {
                EnabledDevices.Remove(removedDevice);
            }

            foreach (var device in enabledDevices)
            {
                if (!EnabledDevices.Any(existing => existing.DeviceId == device.DeviceId))
                {
                    EnabledDevices.Add(device);
                }
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
