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
using System.Windows;
using System.Windows.Threading;

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

        private bool _isBusy;

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _busyMessage = "处理中，请稍候...";

        public string BusyMessage
        {
            get => _busyMessage;
            set => SetProperty(ref _busyMessage, value);
        }

        private bool _isViewActive;

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

                bool hasDataPoints = SelectedDevice.DataPoints.Count > 0;
                bool hasChannelBindings = _recipeConfigService.CurrentRecipe?.Channels.Any(c => c.PlcDeviceId == SelectedDevice.DeviceId) == true;
                bool shouldResetSiemensCache = IsSiemensDeviceType(SelectedDevice.DeviceType)
                    && !IsSiemensDeviceType(value.Value)
                    && (SelectedDevice.SiemensReadCache.IsEnabled || SelectedDevice.SiemensReadCache.FieldDefinitions.Count > 0 || SelectedDevice.SiemensReadCache.ExpandedFieldDefinitions.Count > 0);

                if (hasDataPoints || hasChannelBindings || shouldResetSiemensCache)
                {
                    var resetTargets = new List<string>();
                    if (hasDataPoints)
                    {
                        resetTargets.Add("当前设备的所有点位");
                    }

                    if (hasChannelBindings)
                    {
                        resetTargets.Add("已绑定该设备的通道、点位关联");
                    }

                    if (shouldResetSiemensCache)
                    {
                        resetTargets.Add("西门子缓存配置");
                    }

                    var result = HandyControl.Controls.MessageBox.Show($"切换设备类型会清空{string.Join("、", resetTargets)}，是否继续？", "提示", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);

                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        ResetDeviceBindingsForTypeChange(SelectedDevice, value.Value);
                        SelectedDevice.DeviceType = value.Value;
                    }
                    else
                    {
                        Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            OnPropertyChanged(nameof(SelectedDeviceType));
                        });
                    }
                }
                else
                {
                    SelectedDevice.DeviceType = value.Value;
                }
            }
        }

        private void ResetDeviceBindingsForTypeChange(PlcDevice device, PlcDeviceType newDeviceType)
        {
            device.StopCacheReading();
            device.DataPoints.Clear();
            SelectedDataPoint = null;

            var channels = _recipeConfigService.CurrentRecipe?.Channels;
            if (channels != null)
            {
                foreach (var channel in channels.Where(c => c.PlcDeviceId == device.DeviceId))
                {
                    channel.PlcDeviceId = 0;
                    channel.DataPointId = string.Empty;
                    channel.DataSourceAddress = string.Empty;
                    channel.AvailableDataPoints = [];
                    channel.UseCacheValue = false;
                    channel.WriteBackDataPointIdA = string.Empty;
                    channel.WriteBackDataPointIdB = string.Empty;
                }
            }

            if (IsSiemensDeviceType(device.DeviceType) && !IsSiemensDeviceType(newDeviceType))
            {
                device.SiemensReadCache = new SiemensReadCacheConfig();
            }
        }

        private static bool IsSiemensDeviceType(PlcDeviceType deviceType)
        {
            return deviceType is PlcDeviceType.SiemensS7_1200 or PlcDeviceType.SiemensS7_1500;
        }

        public void SetViewActive(bool isActive)
        {
            _isViewActive = isActive;
            if (!isActive)
            {
                IsPointSettingOpen = false;
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
                        var selectedDeviceId = SelectedDevice?.DeviceId;

                        OnPropertyChanged(nameof(Devices));
                        SelectedDevice = selectedDeviceId.HasValue
                            ? Devices.FirstOrDefault(d => d.DeviceId == selectedDeviceId.Value) ?? Devices.FirstOrDefault()
                            : Devices.FirstOrDefault();
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

        private async Task ExecuteWithLoadingAsync(string message, Func<Task> action)
        {
            IsBusy = true;
            BusyMessage = message;

            if (Application.Current != null)
            {
                await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            }

            try
            {
                await action();
            }
            finally
            {
                IsBusy = false;
            }
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
                await ExecuteWithLoadingAsync("正在切换设备类型并重建设备连接...", async () =>
                {
                    try
                    {
                        await device.InitPlcAsync();
                        _log.Info($"设备类型已更改为: {device.DeviceType}，协议已重新初始化");
                    }
                    catch (Exception ex)
                    {
                        _log.Error($"设备类型切换后初始化失败: {ex.Message}");
                    }
                });

                // 强制刷新 UI
                var currentDevice = SelectedDevice;
                SelectedDevice = null;
                SelectedDevice = currentDevice;
            }
        }

        [RelayCommand]
        private async Task AddDevice()
        {
            await ExecuteWithLoadingAsync("正在添加设备并初始化连接...", async () =>
            {
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

                await newDevice.InitPlcAsync();
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

                if (_isViewActive)
                {
                    IsPointSettingOpen = true;
                    Growl.Success("已添加新设备，请配置设备参数");
                }
                else
                {
                    Growl.Success("已添加新设备");
                }

                _log.Info($"添加设备: {newDevice.DeviceName}");
            });
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

            await ExecuteWithLoadingAsync($"正在删除设备 {deviceName}...", async () =>
            {
                await SelectedDevice.DestroyPlcAsync();

                Devices.Remove(SelectedDevice);
                SelectedDevice = Devices.FirstOrDefault();
                Growl.Info($"已删除设备: {deviceName}");
                _log.Info($"删除设备: {deviceName}");
            });
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

            await ExecuteWithLoadingAsync($"正在测试 {SelectedDevice.DeviceName} 连接...", async () =>
            {
                try
                {
                    await SelectedDevice.InitPlcAsync();

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
            });
        }

        [RelayCommand]
        private async Task SaveConfiguration()
        {
            await ExecuteWithLoadingAsync("正在保存设备配置...", async () =>
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
            });
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

            await ExecuteWithLoadingAsync($"正在应用 {SelectedDevice.DeviceName} 参数并重连...", async () =>
            {
                try
                {
                    await SelectedDevice.InitPlcAsync();

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
            });
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

            await ExecuteWithLoadingAsync("正在保存点位配置...", async () =>
            {
                try
                {
                    CheckAllAddresses();
                    SelectedDevice.ResetDevicePoints();

                    var success = await _recipeConfigService.SaveCurrentRecipeAsync();
                    if (!success)
                    {
                        Growl.Error("点位配置保存到配方失败");
                        return;
                    }

                    Growl.Success("点位配置已保存");
                    IsPointSettingOpen = false;
                }
                catch (Exception ex)
                {
                    Growl.Error($"保存失败: {ex.Message}");
                    _log.Error($"保存点位配置异常: {ex.Message}");
                }
            });
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

        /// <summary>
        /// 验证缓存结构定义并生成对应 DataPoint（真实西门子地址，供寄存器读取）
        /// </summary>
        [RelayCommand]
        private void ApplyCachePoints()
        {
            if (SelectedDevice == null)
            {
                Growl.Warning("请先选择一个设备");
                return;
            }

            var cache = SelectedDevice.SiemensReadCache;
            if (!cache.IsEnabled)
            {
                Growl.Warning("请先启用双缓冲读取");
                return;
            }

            var (valid, msg) = cache.ValidateAndApplyStructure();
            if (!valid)
            {
                Growl.Error(msg);
                return;
            }

            // 检查是否有通道绑定了旧的缓存生成点位
            var oldCachePoints = SelectedDevice.DataPoints.Where(dp => dp.IsCacheGenerated).ToList();
            if (oldCachePoints.Count > 0)
            {
                var oldPointIds = oldCachePoints.Select(dp => dp.PointId).ToHashSet();
                var channels = _recipeConfigService.CurrentRecipe?.Channels;
                var boundChannels = channels?
                    .Where(c => c.PlcDeviceId == SelectedDevice.DeviceId && oldPointIds.Contains(c.DataPointId))
                    .ToList();

                if (boundChannels != null && boundChannels.Count > 0)
                {
                    var channelNames = string.Join("、", boundChannels.Select(c => c.ChannelName));
                    var result = HandyControl.Controls.MessageBox.Show(
                        $"以下通道已绑定了缓存点位，重新生成后原绑定将被清除：\n\n{channelNames}\n\n是否继续？",
                        "提示",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes)
                        return;

                    // 清除绑定的通道引用（包括设备ID）
                    foreach (var ch in boundChannels)
                    {
                        ch.PlcDeviceId = 0;
                        ch.DataPointId = string.Empty;
                        ch.DataSourceAddress = string.Empty;
                        ch.UseCacheValue = false;
                        ch.AvailableDataPoints = new ObservableCollection<DataPoint>();
                    }
                    _log.Info($"已清除 {boundChannels.Count} 个通道的缓存点位绑定");
                }
            }

            // 移除旧的缓存生成点位
            foreach (var dp in oldCachePoints)
                SelectedDevice.DataPoints.Remove(dp);

            // 生成真实地址的 DataPoint（缓存1/缓存2分开生成）
            int nextId = SelectedDevice.DataPoints.Count > 0
                ? SelectedDevice.DataPoints.Max(p => int.TryParse(p.PointId, out int id) ? id : 0) + 1
                : 1;

            for (int cacheIndex = 1; cacheIndex <= 2; cacheIndex++)
            {
                string dbBlock = cacheIndex == 1 ? cache.Cache1.DbBlock : cache.Cache2.DbBlock;
                for (int g = 0; g < cache.GroupCount; g++)
                {
                    int groupBaseOffset = g * cache.GroupSize;
                    foreach (var field in cache.FieldDefinitions)
                    {
                        int actualOffset = groupBaseOffset + field.Offset;
                        string cacheSuffix = $"_C{cacheIndex}";
                        string groupSuffix = cache.GroupCount > 1 ? $"_G{g + 1}" : "";
                        string address = GetSiemensDbAddress(dbBlock, actualOffset, field.DataType);

                        var dp = new DataPoint
                        {
                            PointId = nextId.ToString(),
                            PointName = $"{field.FieldName}{cacheSuffix}{groupSuffix}",
                            Address = address,
                            DataType = field.DataType,
                            ByteOrder = field.ByteOrder,
                            IsEnabled = true,
                            IsCacheGenerated = true,
                            CacheFieldKey = $"CACHE:C{cacheIndex}:G{g}:{field.FieldName}"
                        };
                        SelectedDevice.DataPoints.Add(dp);
                        nextId++;
                    }
                }
            }
            CheckAllAddresses();
            // 打开点位设置，定位到第一个生成的点位
            var firstGenerated = SelectedDevice.DataPoints.FirstOrDefault(dp => dp.IsCacheGenerated);
            if (firstGenerated != null)
            {
                IsPointSettingOpen = true;
                SelectedDataPoint = firstGenerated;
            }

            // 下发点位到下位机
            SelectedDevice.ResetDevicePoints();
            SelectedDevice.StopCacheReading();
            SelectedDevice.StartCacheReading();
            int totalPoints = cache.FieldDefinitions.Count * cache.GroupCount * 2;
            Growl.Success($"{msg}，已生成 {totalPoints} 个点位（可在点位设置中编辑地址）");
            _log.Info($"设备 [{SelectedDevice.DeviceName}] 缓存结构验证通过，生成 {totalPoints} 个点位");
        }

        /// <summary>
        /// 双击缓存字段 Grid 行 → 打开点位设置并定位到对应点位
        /// </summary>
        [RelayCommand]
        private void OpenCacheFieldPoint(CacheFieldDefinition? field)
        {
            if (field == null || SelectedDevice == null) return;

            // 通过 CacheFieldKey 精确匹配点位
            var dp = SelectedDevice.DataPoints.FirstOrDefault(d =>
                d.IsCacheGenerated && d.CacheFieldKey == field.CacheFieldKey);

            // 回退：按 DisplayName 匹配
            dp ??= SelectedDevice.DataPoints.FirstOrDefault(d =>
                d.IsCacheGenerated && d.PointName == field.DisplayName);

            if (dp != null)
            {
                IsPointSettingOpen = true;
                SelectedDataPoint = dp;
            }
            else
            {
                var result = HandyControl.Controls.MessageBox.Show($"未找到缓存字段 [{field.DisplayName}] 对应的点位，是否立即创建？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                dp = CreateCacheFieldPoint(field);
                if (dp == null)
                {
                    Growl.Warning("创建对应点位失败");
                    return;
                }

                IsPointSettingOpen = true;
                SelectedDataPoint = dp;
            }
        }

        private DataPoint? CreateCacheFieldPoint(CacheFieldDefinition field)
        {
            if (SelectedDevice == null) return null;

            var cache = SelectedDevice.SiemensReadCache;
            string dbBlock = field.CacheIndex == 1 ? cache.Cache1.DbBlock : cache.Cache2.DbBlock;
            int actualOffset = field.GroupIndex * cache.GroupSize + field.Offset;
            string address = GetSiemensDbAddress(dbBlock, actualOffset, field.DataType);

            int nextId = SelectedDevice.DataPoints.Count > 0
                ? SelectedDevice.DataPoints.Max(p => int.TryParse(p.PointId, out int id) ? id : 0) + 1
                : 1;

            var dataPoint = new DataPoint
            {
                PointId = nextId.ToString(),
                PointName = string.IsNullOrWhiteSpace(field.DisplayName) ? field.FieldName : field.DisplayName,
                Address = address,
                DataType = field.DataType,
                ByteOrder = field.ByteOrder,
                IsEnabled = true,
                IsCacheGenerated = true,
                CacheFieldKey = field.CacheFieldKey
            };

            ValidateDataPoint(dataPoint, SelectedDevice.DeviceType, out _);
            SelectedDevice.DataPoints.Add(dataPoint);
            SelectedDevice.ResetDevicePoints();

            _log.Info($"已为缓存字段 [{field.DisplayName}] 自动创建点位 [{dataPoint.PointName}]，地址: {dataPoint.Address}");
            return dataPoint;
        }

        /// <summary>
        /// 根据 DB 块、偏移和数据类型生成西门子地址
        /// </summary>
        private static string GetSiemensDbAddress(string dbBlock, int offset, FieldType dataType)
        {
            string blockPart = dbBlock;
            int baseOffset = 0;

            if (!string.IsNullOrWhiteSpace(dbBlock))
            {
                var parts = dbBlock.Split('.', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    blockPart = parts[0];
                }

                if (parts.Length > 1)
                {
                    int.TryParse(parts[1], out baseOffset);
                }
            }

            int actualOffset = baseOffset + offset;

            string suffix = dataType switch
            {
                FieldType.Bool => $"DBX{actualOffset}.0",
                FieldType.Byte => $"DBB{actualOffset}",
                FieldType.Int16 or FieldType.UInt16 or FieldType.Char => $"DBW{actualOffset}",
                FieldType.Int32 or FieldType.UInt32 or FieldType.Float => $"DBD{actualOffset}",
                FieldType.Int64 or FieldType.UInt64 or FieldType.Long or FieldType.Double => $"DBB{actualOffset}",
                _ => $"DBB{actualOffset}"
            };
            return $"{blockPart}.{suffix}";
        }


    }
}
