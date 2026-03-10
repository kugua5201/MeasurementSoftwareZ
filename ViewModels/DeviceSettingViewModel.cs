using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using MeasurementSoftware.Extensions;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Config;
using MeasurementSoftware.Services.Logs;
using MultiProtocol.Model;
using MultiProtocol.Services.Siemens;
using MultiProtocol.Utils;
using System.Collections.ObjectModel;
using System.IO.Ports;

namespace MeasurementSoftware.ViewModels
{
    public partial class DeviceSettingViewModel : ObservableViewModel
    {
        private readonly ILog _log;
        private readonly IDeviceConfigService _deviceConfigService;
        private readonly IRecipeConfigService _recipeConfigService;

        /// <summary>
        /// 设备集合
        /// </summary>
        public ObservableCollection<PlcDevice> Devices => _deviceConfigService.Devices;

        [ObservableProperty]
        private PlcDevice? selectedDevice;

        [ObservableProperty]
        private DataPoint? selectedDataPoint;

        [ObservableProperty]
        private bool isPointSettingOpen;

        /// <summary>
        /// 可用串口列表（供 Modbus RTU 模板绑定）
        /// </summary>
        public ObservableCollection<string> AvailableComPorts { get; } = new();

        public PlcDeviceType? SelectedDeviceType
        {
            get => SelectedDevice?.DeviceType;
            set
            {
                if (SelectedDevice == null || value == null || SelectedDevice.DeviceType == value)
                    return;

                if (SelectedDevice.DataPoints.Count > 0)
                {
                    var result = HandyControl.Controls.MessageBox.Show(
                        "切换设备类型会清空所有点位，是否继续？",
                        "提示",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Warning);

                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        SelectedDevice.DeviceType = value.Value;
                        SelectedDevice.DataPoints.Clear();
                    }
                    else
                    {
                        // 异步通知UI回退到旧值，必须用Dispatcher延迟触发，
                        // 否则ComboBox在同步调用栈中不会刷新已选项
                        OnPropertyChanged(nameof(SelectedDeviceType));
                    }
                }
                else
                {
                    SelectedDevice.DeviceType = value.Value;
                }
            }
        }

        public ObservableCollection<PlcDeviceType> DeviceTypes { get; } = new ObservableCollection<PlcDeviceType>(Enum.GetValues<PlcDeviceType>().Cast<PlcDeviceType>());

        public DeviceSettingViewModel(ILog log, IDeviceConfigService deviceConfigService, IRecipeConfigService recipeConfigService)
        {
            _log = log;
            _deviceConfigService = deviceConfigService;
            _recipeConfigService = recipeConfigService;

            // 监听配置服务中的设备集合变化（比如新建配方、打开配方导致的 Devices 实例替换）
            if (_deviceConfigService is System.ComponentModel.INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(IDeviceConfigService.Devices))
                    {
                        OnPropertyChanged(nameof(Devices));
                        SelectedDevice = Devices.FirstOrDefault();
                    }
                };
            }


            // 如果有设备，默认选择第一个
            if (Devices.Count > 0)
            {
                SelectedDevice = Devices[0];
            }
            RefreshComPorts();
        }

        /// <summary>
        /// 刷新可用串口列表
        /// </summary>
        [RelayCommand]
        private void RefreshComPorts()
        {
            AvailableComPorts.Clear();
            foreach (var port in SerialPort.GetPortNames())
            {
                AvailableComPorts.Add(port);
            }
            if (AvailableComPorts.Count > 0)
            {
                SelectedDevice?.ComPort = AvailableComPorts[0];
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

            OnPropertyChanged(nameof(SelectedDeviceType));
        }

        private async void Device_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlcDevice.DeviceType) && sender is PlcDevice device)
            {
                OnPropertyChanged(nameof(SelectedDeviceType));

                // 设备类型改变时，重新初始化PLC协议实例
                try
                {
                    await device.InitPlcAsync();
                    _log.Info($"设备类型已更改为: {device.DeviceType}，协议已重新初始化");
                }
                catch (Exception ex)
                {
                    _log.Error($"设备类型切换后初始化失败: {ex.Message}");
                }

                // 强制刷新 UI
                var currentDevice = SelectedDevice;
                SelectedDevice = null;
                SelectedDevice = currentDevice;
            }
        }

        [RelayCommand]
        private async Task AddDevice()
        {
            // 计算新设备的ID（自增）
            long newDeviceId = Devices.Count > 0 ? Devices.Max(d => d.DeviceId) + 1 : 1;

            var newDevice = new PlcDevice
            {
                DeviceId = newDeviceId,
                DeviceName = $"PLC设备{newDeviceId}",
                DeviceType = PlcDeviceType.SiemensS7_1200,
                IpAddress = "192.168.0.1",
                Port = 102,
                IsEnabled = false
            };

            // 初始化协议实例并连接（默认IsEnabled=false，不会Open轮询）
            newDevice.InitPlc();
            var (success, message) = await newDevice.ConnectAsync();
            if (success)
            {
                _log.Info($"新设备 [{newDevice.DeviceName}] 连接成功");
            }
            else
            {
                _log.Warn($"新设备 [{newDevice.DeviceName}] 连接失败: {message}");
            }

            Devices.Add(newDevice);
            SelectedDevice = newDevice;
            OnPropertyChanged(nameof(Devices));

            IsPointSettingOpen = true;
            Growl.Success("已添加新设备，请配置设备参数");
            _log.Info($"添加设备: {newDevice.DeviceName}");
        }

        [RelayCommand]
        private async Task DeleteDevice()
        {
            if (SelectedDevice == null)
            {
                Growl.Warning("请选择要删除的设备");
                return;
            }
            var channels = _recipeConfigService.CurrentRecipe?.Channels;
            if (channels != null)
            {
                bool hasBind = channels.Any(c => c.PlcDeviceId == SelectedDevice.DeviceId);
                if (hasBind)
                {
                    Growl.Warning("当前设备已被通道绑定，不能删除！");
                    return;
                }

            }
            var deviceName = SelectedDevice.DeviceName;

            // 销毁PLC协议实例，释放连接资源
            await SelectedDevice.DestroyPlcAsync();

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

            try
            {
                // 每次测试都用当前最新参数重建协议实例（参数已双向绑定）
                await SelectedDevice.InitPlcAsync();

                // 连接 → 内部会 SetDevice 注册点位 → 如果IsEnabled则Open开始轮询
                var (success, message) = await SelectedDevice.ConnectAsync();
                if (success)
                {
                    Growl.Success($"{SelectedDevice.DeviceName} 连接成功");
                    _log.Info($"测试连接成功: {SelectedDevice.DeviceName}");
                }
                else
                {
                    Growl.Error($"{SelectedDevice.DeviceName} {message}");
                    _log.Warn($"测试连接失败: {SelectedDevice.DeviceName} - {message}");
                }
            }
            catch (Exception ex)
            {
                Growl.Error($"连接异常: {ex.Message}");
                _log.Error($"测试连接异常: {SelectedDevice.DeviceName} - {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task SaveConfiguration()
        {
            try
            {
                var success = await _deviceConfigService.SaveDevicesAsync([.. Devices]);
                if (success)
                {
                    Growl.Success($"设备配置已保存");
                    _log.Info($"保存设备配置成功");
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

        /// <summary>
        /// 保存当前设备配置并重新连接（修改参数后应用）
        /// </summary>
        [RelayCommand]
        private async Task ApplyAndReconnect()
        {
            if (SelectedDevice == null)
            {
                Growl.Warning("请先选择一个设备");
                return;
            }

            try
            {
                // 1. 保存配置到本地
                //var saveSuccess = await _deviceConfigService.SaveDevicesAsync(Devices.ToList());
                //if (!saveSuccess)
                //{
                //    Growl.Error("保存配置失败");
                //    return;
                //}

                //Growl.Info($"正在重新连接 {SelectedDevice.DeviceName}...");

                // 2. 用最新参数重建协议实例
                await SelectedDevice.InitPlcAsync();

                // 3. 连接（内部会 SetDevice + 如果IsEnabled则Open）
                var (success, message) = await SelectedDevice.ConnectAsync();
                if (success)
                {
                    Growl.Success($"{SelectedDevice.DeviceName} 配置已保存，重连成功");
                    _log.Info($"设备 [{SelectedDevice.DeviceName}] 参数应用并重连成功");
                }
                else
                {
                    Growl.Warning($"配置已保存，但连接失败: {message}");
                    _log.Warn($"设备 [{SelectedDevice.DeviceName}] 参数应用后连接失败: {message}");
                }
            }
            catch (Exception ex)
            {
                Growl.Error($"应用参数失败: {ex.Message}");
                _log.Error($"设备 [{SelectedDevice?.DeviceName}] 应用参数异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 配置点位
        /// </summary>
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
                Address = GetDefaultAddress(SelectedDevice.DeviceType),
                DataType = FieldType.Float,
                ByteOrder = ByteOrder.DCBA,
                IsEnabled = true
            };

            // 验证新添加的点位
            if (!ValidateDataPoint(newPoint, SelectedDevice.DeviceType, out string error))
            {
                Growl.Warning($"默认地址验证失败: {error}，请修改后保存");
            }

            SelectedDevice.DataPoints.Add(newPoint);
            SelectedDataPoint = newPoint;
            //Growl.Success("已添加新数据点");
            //_log.Info($"添加数据点: {newPoint.PointName}");
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
            //Growl.Info($"已删除数据点: {pointName}");
            //_log.Info($"删除数据点: {pointName}");
        }

        [RelayCommand]
        private void CheckAllAddresses()
        {
            if (SelectedDevice == null)
            {
                Growl.Warning("请先选择一个设备");
                return;
            }

            if (SelectedDevice.DataPoints.Count == 0)
            {
                Growl.Warning("当前设备没有配置数据点");
                return;
            }

            int successCount = 0;
            int failCount = 0;

            foreach (var dataPoint in SelectedDevice.DataPoints)
            {
                if (ValidateDataPoint(dataPoint, SelectedDevice.DeviceType, out _))
                {
                    successCount++;
                }
                else
                {
                    failCount++;
                }
            }

            if (failCount == 0)
            {
                Growl.Success($"所有 {successCount} 个点位地址验证通过");
            }
            else
            {
                Growl.Warning($"验证完成: {successCount} 个通过，{failCount} 个失败，请检查失败项");
            }

            _log.Info($"设备 {SelectedDevice.DeviceName} 地址检查完成: 成功{successCount}, 失败{failCount}");
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

                SelectedDevice.ResetDevicePoints();
                Growl.Success("点位配置已保存");
                IsPointSettingOpen = false;
            }
            catch (Exception ex)
            {
                Growl.Error($"保存失败: {ex.Message}");
                _log.Error($"保存点位配置异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 验证数据点地址格式
        /// </summary>
        private bool ValidateDataPoint(DataPoint dataPoint, PlcDeviceType deviceType, out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(dataPoint.Address))
            {
                error = "地址不能为空";
                dataPoint.ValidationStatus = "❌ 失败";
                dataPoint.ValidationError = error;
                dataPoint.IsValidated = false;
                return false;
            }

            try
            {
                var driveType = GetDriveType(deviceType);
                var fieldInfo = new FieldInfo(dataPoint.Address, dataPoint.DataType, dataPoint.ByteOrder)
                {
                    Alias = dataPoint.PointName
                };
                var fields = new List<FieldInfo> { fieldInfo };

                // 使用 MultiProtocol 库验证地址
                var checkedFields = DataFieldsHelper.CheckFileds(driveType, fields);

                if (checkedFields == null || checkedFields.Count == 0)
                {
                    error = $"地址 '{dataPoint.Address}' 格式不正确";
                    dataPoint.ValidationStatus = "❌ 失败";
                    dataPoint.ValidationError = error;
                    dataPoint.IsValidated = false;
                    return false;
                }

                // 检查返回的字段是否有错误
                var resultField = checkedFields.FirstOrDefault();
                if (!resultField?.IsSuccess == true)
                {
                    error = $"{resultField?.Message}";
                    dataPoint.ValidationStatus = "❌ 失败";
                    dataPoint.ValidationError = error;
                    dataPoint.IsValidated = false;
                    return false;
                }

                // 验证成功
                dataPoint.ValidationStatus = "✅ 通过";
                dataPoint.ValidationError = string.Empty;
                dataPoint.IsValidated = true;
                return true;
            }
            catch (Exception ex)
            {
                error = $"验证异常: {ex.Message}";
                dataPoint.ValidationStatus = "⚠️ 异常";
                dataPoint.ValidationError = error;
                dataPoint.IsValidated = false;
                return false;
            }
        }

        /// <summary>
        /// 获取设备类型对应的 DriveType
        /// </summary>
        private DriveType GetDriveType(PlcDeviceType deviceType)
        {
            return deviceType switch
            {
                PlcDeviceType.SiemensS7_1200 => DriveType.SiemensS7_1200,
                PlcDeviceType.SiemensS7_1500 => DriveType.SiemensS7_1500,
                PlcDeviceType.MitsubishiMC => DriveType.MitsubishiMcBinary,
                PlcDeviceType.ModbusTCP => DriveType.ModbusTcpNet,
                PlcDeviceType.ModbusRTU => DriveType.ModbusRtu,
                _ => DriveType.ModbusTcpNet
            };
        }

        /// <summary>
        /// 根据设备类型获取默认地址
        /// </summary>
        private string GetDefaultAddress(PlcDeviceType deviceType)
        {
            return deviceType switch
            {
                PlcDeviceType.SiemensS7_1200 => "DB1.DBD0",
                PlcDeviceType.SiemensS7_1500 => "DB1.DBD0",
                PlcDeviceType.MitsubishiMC => "D100",
                PlcDeviceType.ModbusTCP => "40001",
                PlcDeviceType.ModbusRTU => "40001",
                _ => "DB1.DBD0"
            };
        }

        /// <summary>
        /// 重新编号
        /// </summary>
        [RelayCommand]
        public void RenumberPoints()
        {
            if (SelectedDevice == null)
            {
                Growl.Warning("请先选择一个设备");
                return;
            }

            for (int i = 0; i < SelectedDevice.DataPoints.Count; i++)
            {
                SelectedDevice.DataPoints[i].PointId = (i + 1).ToString();
            }

        }
    }
}
