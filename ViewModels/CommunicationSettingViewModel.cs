using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Logs;
using MeasurementSoftware.Services;
using System.Collections.ObjectModel;
using HandyControl.Controls;

namespace MeasurementSoftware.ViewModels
{
    public partial class CommunicationSettingViewModel : ObservableViewModel
    {
        private readonly ILog _log;
        private readonly IDeviceConfigService _deviceConfigService;

        // 直接引用全局配置的设备集合
        public ObservableCollection<PlcDevice> Devices => _deviceConfigService.Devices;

        [ObservableProperty]
        private PlcDevice? selectedDevice;

        [ObservableProperty]
        private DataPoint? selectedDataPoint;

        [ObservableProperty]
        private bool isPointSettingOpen;

        public ObservableCollection<PlcDeviceType> DeviceTypes { get; } = new()
        {
            PlcDeviceType.SiemensS7,
            PlcDeviceType.MitsubishiMC,
            PlcDeviceType.ModbusTCP,
            PlcDeviceType.ModbusRTU
        };

        public CommunicationSettingViewModel(ILog log, IDeviceConfigService deviceConfigService)
        {
            _log = log;
            _deviceConfigService = deviceConfigService;
            // 如果有设备，默认选择第一个
            if (Devices.Count > 0)
            {
                SelectedDevice = Devices[0];
            }
        }

        partial void OnSelectedDeviceChanged(PlcDevice? oldValue, PlcDevice? newValue)
        {
            // 取消旧设备的属性变化监听
            if (oldValue != null)
            {
                oldValue.PropertyChanged -= Device_PropertyChanged;
            }

            // 订阅新设备的属性变化
            if (newValue != null)
            {
                newValue.PropertyChanged += Device_PropertyChanged;
                // 切换设备时，自动选中第一个点位
                SelectedDataPoint = newValue.DataPoints.FirstOrDefault();
            }
            else
            {
                SelectedDataPoint = null;
            }
        }

        private void Device_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlcDevice.DeviceType) && sender is PlcDevice device)
            {
                // 设备类型改变时，强制刷新 UI - 先设为null再设回来
                var currentDevice = SelectedDevice;
                SelectedDevice = null;
                SelectedDevice = currentDevice;

                _log.Info($"设备类型已更改为: {device.DeviceType}");
            }
        }

        [RelayCommand]
        private void AddDevice()
        {
            // 计算新设备的ID（自增）
            int newDeviceId = Devices.Count > 0 ? Devices.Max(d => int.TryParse(d.DeviceId, out int id) ? id : 0) + 1 : 1;

            var newDevice = new PlcDevice
            {
                DeviceId = newDeviceId.ToString(),
                DeviceName = $"PLC设备{newDeviceId}",
                DeviceType = PlcDeviceType.SiemensS7,
                IpAddress = "192.168.0.1",
                Port = 102,
                IsEnabled = true
            };
            Devices.Add(newDevice);
            SelectedDevice = newDevice;
            Growl.Success("已添加新设备，请配置设备参数");
            _log.Info($"添加设备: {newDevice.DeviceName}");
        }

        [RelayCommand]
        private void DeleteDevice()
        {
            if (SelectedDevice == null)
            {
                Growl.Warning("请选择要删除的设备");
                return;
            }

            var deviceName = SelectedDevice.DeviceName;
            Devices.Remove(SelectedDevice);
            SelectedDevice = Devices.FirstOrDefault();
            Growl.Info($"已删除设备: {deviceName}");
            _log.Info($"删除设备: {deviceName}");
        }

        [RelayCommand]
        private async Task TestConnection()
        {
            if (SelectedDevice == null)
            {
                Growl.Warning("请选择要测试的设备");
                return;
            }

            Growl.Info($"正在测试 {SelectedDevice.DeviceName} 连接...");
            await Task.Delay(1000);

            // TODO: 实际的连接测试逻辑
            SelectedDevice.IsConnected = true;
            Growl.Success($"{SelectedDevice.DeviceName} 连接成功");
            _log.Info($"测试连接: {SelectedDevice.DeviceName}");
        }

        [RelayCommand]
        private async Task SaveConfiguration()
        {
            try
            {
                var success = await _deviceConfigService.SaveDevicesAsync(Devices.ToList());
                if (success)
                {
                    Growl.Success("设备配置已保存");
                    _log.Info("保存设备配置成功");
                }
                else
                {
                    Growl.Error("保存配置失败");
                    _log.Error("保存设备配置失败");
                }
            }
            catch (Exception ex)
            {
                Growl.Error($"保存失败: {ex.Message}");
                _log.Error($"保存设备配置异常: {ex.Message}");
            }
        }

        [RelayCommand]
        private void OpenPointSetting()
        {
            if (SelectedDevice == null)
            {
                Growl.Warning("请先选择一个设备");
                return;
            }

            IsPointSettingOpen = true;
            _log.Info($"打开设备 {SelectedDevice.DeviceName} 的点位配置");
        }

        [RelayCommand]
        private void AddDataPoint()
        {
            if (SelectedDevice == null)
            {
                Growl.Warning("请先选择一个设备");
                return;
            }

            // 计算新点位的ID（自增）
            int newPointId = SelectedDevice.DataPoints.Count > 0
                ? SelectedDevice.DataPoints.Max(p => int.TryParse(p.PointId, out int id) ? id : 0) + 1
                : 1;

            var newPoint = new DataPoint
            {
                PointId = newPointId.ToString(),
                PointName = $"点位{newPointId}",
                Address = "DB1.DBD0",
                DataType = "Float",
                IsEnabled = true
            };

            SelectedDevice.DataPoints.Add(newPoint);
            SelectedDataPoint = newPoint;
            Growl.Success("已添加新数据点");
            _log.Info($"添加数据点: {newPoint.PointName}");
        }

        [RelayCommand]
        private void DeleteDataPoint()
        {
            if (SelectedDevice == null || SelectedDataPoint == null)
            {
                Growl.Warning("请选择要删除的数据点");
                return;
            }

            var pointName = SelectedDataPoint.PointName;
            SelectedDevice.DataPoints.Remove(SelectedDataPoint);
            SelectedDataPoint = SelectedDevice.DataPoints.FirstOrDefault();
            Growl.Info($"已删除数据点: {pointName}");
            _log.Info($"删除数据点: {pointName}");
        }

        [RelayCommand]
        private async Task SavePointConfiguration()
        {
            if (SelectedDevice == null)
            {
                Growl.Warning("没有选中的设备");
                return;
            }

            try
            {
                // 保存所有设备配置（包含点位）
                var success = await _deviceConfigService.SaveDevicesAsync(Devices.ToList());
                if (success)
                {
                    Growl.Success("点位配置已保存");
                    _log.Info($"保存设备 {SelectedDevice.DeviceName} 的点位配置成功");
                }
                else
                {
                    Growl.Error("保存点位配置失败");
                    _log.Error("保存点位配置失败");
                }
            }
            catch (Exception ex)
            {
                Growl.Error($"保存失败: {ex.Message}");
                _log.Error($"保存点位配置异常: {ex.Message}");
            }
        }
    }
}
