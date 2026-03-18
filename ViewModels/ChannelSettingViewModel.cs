using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Logs;
using System.Collections.ObjectModel;
using System.Windows;
using HandyControl.Controls;
using MeasurementSoftware.Services.Config;
using MessageBox = HandyControl.Controls.MessageBox;

namespace MeasurementSoftware.ViewModels
{
    public partial class ChannelSettingViewModel : ObservableViewModel
    {
        private readonly ILog _log;
        private readonly IRecipeConfigService _recipeConfigService;
        private readonly IDeviceConfigService _deviceConfigService;

        // 直接引用全局配置
        public MeasurementRecipe? CurrentRecipe => _recipeConfigService.CurrentRecipe;
        public MeasurementRecipe? SelectedRecipe => _recipeConfigService.CurrentRecipe;
        public ObservableCollection<MeasurementChannel> Channels => CurrentRecipe?.Channels ?? [];

        /// <summary>
        /// 产品图片路径
        /// </summary>
        public string? ProductImagePath => CurrentRecipe?.ProductImagePath;

        /// <summary>
        /// 标注点集合（从通道中聚合）
        /// </summary>
        public IEnumerable<ChannelAnnotation> Annotations => CurrentRecipe?.Channels?.Where(c => c.Annotation != null).Select(c => c.Annotation!) ?? Enumerable.Empty<ChannelAnnotation>();

        /// <summary>
        /// 选中的标注点
        /// </summary>
        [ObservableProperty]
        private ChannelAnnotation? selectedAnnotation;

        /// <summary>
        /// 右键点击的图片坐标（用于添加标注）
        /// </summary>
        [ObservableProperty]
        private double clickX;

        /// <summary>
        /// 右键点击的图片坐标（用于添加标注）
        /// </summary>
        [ObservableProperty]
        private double clickY;

        /// <summary>
        /// 可用的PLC设备列表（仅包含已启用的设备）
        /// </summary>
        public IEnumerable<PlcDevice> AvailablePlcDevices => _deviceConfigService.Devices.Where(d => d.IsEnabled);

        [ObservableProperty]
        private MeasurementChannel? selectedChannel;

        [ObservableProperty]
        private bool isChannelEditorOpen;

        [ObservableProperty]
        private MeasurementChannel? editingChannel;

        [ObservableProperty]
        private bool isEditMode;

        /// <summary>
        /// 是否显示"使用缓存值"开关（当前编辑通道的点位是缓存生成的时候才显示）
        /// </summary>
        [ObservableProperty]
        private bool showCacheToggle;

        /// <summary>
        /// 抽屉标题（根据是添加还是编辑动态显示）
        /// </summary>
        public string DrawerTitle => IsEditMode ? "编辑通道" : "添加通道";

        public IEnumerable<ChannelType> ChannelTypes => Enum.GetValues<ChannelType>();

        private ObservableCollection<PlcDevice>? _lastDevices;

        public ChannelSettingViewModel(ILog log, IRecipeConfigService recipeConfigService, IDeviceConfigService deviceConfigService)
        {
            _log = log;
            _recipeConfigService = recipeConfigService;
            _deviceConfigService = deviceConfigService;

            // 监听配方和设备变化
            if (_recipeConfigService is System.ComponentModel.INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(IRecipeConfigService.CurrentRecipe))
                    {
                        OnRecipeChanged();
                    }
                    else if (e.PropertyName == nameof(IDeviceConfigService.Devices))
                    {
                        OnDevicesChanged();
                    }
                };
            }

            OnDevicesChanged();
            OnRecipeChanged();
        }

        private void OnDevicesChanged()
        {
            if (_lastDevices != null)
            {
                _lastDevices.CollectionChanged -= Devices_CollectionChanged;
                foreach (var device in _lastDevices)
                {
                    device.PropertyChanged -= Device_PropertyChanged;
                }
            }

            _lastDevices = _deviceConfigService.Devices;

            if (_lastDevices != null)
            {
                _lastDevices.CollectionChanged += Devices_CollectionChanged;
                foreach (var device in _lastDevices)
                {
                    device.PropertyChanged += Device_PropertyChanged;
                }
            }

            OnPropertyChanged(nameof(AvailablePlcDevices));
        }

        /// <summary>
        /// 配方切换时，刷新通道列表并为每个通道加载数据点
        /// </summary>
        private void OnRecipeChanged()
        {
            if (CurrentRecipe != null)
            {
                foreach (var channel in CurrentRecipe.Channels)
                {
                    channel.PropertyChanged -= Channel_PropertyChanged;
                    channel.PropertyChanged += Channel_PropertyChanged;
                    if (channel.PlcDeviceId != 0)
                    {
                        LoadDataPointsForChannel(channel);
                    }
                }
            }

            OnPropertyChanged(nameof(CurrentRecipe));
            OnPropertyChanged(nameof(SelectedRecipe));
            OnPropertyChanged(nameof(Channels));
            OnPropertyChanged(nameof(AvailablePlcDevices));
            OnPropertyChanged(nameof(ProductImagePath));
            OnPropertyChanged(nameof(Annotations));
        }

        [RelayCommand]
        private async Task SaveRecipeAsync()
        {
            if (CurrentRecipe == null)
            {
                Growl.Warning("没有配方需要保存");
                return;
            }

            CurrentRecipe.ModifyTime = DateTime.Now;
            var success = await _recipeConfigService.SaveCurrentRecipeAsync();
            if (success)
                Growl.Success("配方保存成功");
            else
                Growl.Error("配方保存失败");
        }

        private void Channel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MeasurementChannel.PlcDeviceId) && sender is MeasurementChannel channel)
            {
                LoadDataPointsForChannel(channel);
                channel.DataPointId = string.Empty;
                channel.DataSourceAddress = string.Empty;
            }
            else if (e.PropertyName == nameof(MeasurementChannel.DataPointId) && sender is MeasurementChannel channel2)
            {
                var dataPoint = channel2.AvailableDataPoints.FirstOrDefault(dp => dp.PointId == channel2.DataPointId);
                if (dataPoint != null)
                {
                    channel2.DataSourceAddress = dataPoint.Address;
                }
            }
        }

        private void LoadDataPointsForChannel(MeasurementChannel channel)
        {
            if (channel.PlcDeviceId == 0)
            {
                channel.AvailableDataPoints = new ObservableCollection<DataPoint>();
                return;
            }

            var dataPoints = _deviceConfigService.GetDataPointsByDeviceId(channel.PlcDeviceId);
            channel.AvailableDataPoints = new ObservableCollection<DataPoint>(
                dataPoints.Where(dp => dp.IsEnabled)
                    .OrderBy(dp => int.TryParse(dp.PointId, out int id) ? id : int.MaxValue));
        }

        /// <summary>
        /// 判断设备是否是西门子 S7-1200/1500 且启用了缓存
        /// </summary>
        private bool IsCacheEnabledForDevice(long deviceId)
        {
            var device = _deviceConfigService.Devices.FirstOrDefault(d => d.DeviceId == deviceId);
            if (device == null) return false;
            if (device.DeviceType is not (PlcDeviceType.SiemensS7_1200 or PlcDeviceType.SiemensS7_1500))
                return false;
            return device.SiemensReadCache.IsEnabled && device.SiemensReadCache.IsStructureValid;
        }

        /// <summary>
        /// 设备集合变化时的处理
        /// </summary>
        private void Devices_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // 新增的设备，订阅其属性变化事件
            if (e.NewItems != null)
            {
                foreach (PlcDevice device in e.NewItems)
                {
                    device.PropertyChanged += Device_PropertyChanged;
                }
            }

            // 移除的设备，取消订阅
            if (e.OldItems != null)
            {
                foreach (PlcDevice device in e.OldItems)
                {
                    device.PropertyChanged -= Device_PropertyChanged;
                }
            }

            // 通知设备列表变化
            OnPropertyChanged(nameof(AvailablePlcDevices));
        }

        /// <summary>
        /// 设备属性变化时的处理（特别是 IsEnabled 属性）
        /// </summary>
        private void Device_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlcDevice.IsEnabled) && sender is PlcDevice)
            {
                OnPropertyChanged(nameof(AvailablePlcDevices));
                OnPropertyChanged(nameof(EditingChannel.AvailableDataPoints));
                OnPropertyChanged(nameof(EditingChannel.DataPointId));

                // 遍历所有通道，移除禁用设备绑定
                //foreach (var channel in Channels)
                //{
                //    if (channel.PlcDeviceId == device.DeviceId && !device.IsEnabled)
                //    {
                //        channel.PlcDeviceId = 0;
                //        channel.DataPointId = string.Empty;
                //        channel.DataSourceAddress = string.Empty;
                //        channel.AvailableDataPoints = new ObservableCollection<DataPoint>();
                //    }
                //}
                OnPropertyChanged(nameof(Channels));
            }
        }

        [RelayCommand]
        private void AddChannel()
        {
            if (CurrentRecipe == null)
            {
                Growl.Warning("请先选择一个配方");
                return;
            }

            ShowCacheToggle = false;

            // 创建新通道并打开编辑抽屉
            EditingChannel = new MeasurementChannel
            {
                ChannelNumber = CurrentRecipe.Channels.Count + 1,
                ChannelName = $"通道{CurrentRecipe.Channels.Count + 1}",
                IsEnabled = true,
                RequiresCalibration = false,
                StandardValue = 0,
                UpperTolerance = 0.1,
                LowerTolerance = 0.1,
                // 默认选中第一个通道类型
                ChannelType = ChannelTypes.FirstOrDefault(),
                // 默认选中第一个PLC设备（如果有）
                PlcDeviceId = AvailablePlcDevices.FirstOrDefault()?.DeviceId ?? 0,
            };

            // 加载数据点并设置默认值（如果有设备）
            if (EditingChannel.PlcDeviceId != 0)
            {
                LoadDataPointsForChannel(EditingChannel);
                // 默认选中第一个数据点
                if (EditingChannel.AvailableDataPoints.Any())
                {
                    var firstPoint = EditingChannel.AvailableDataPoints.First();
                    EditingChannel.DataPointId = firstPoint.PointId;
                    EditingChannel.DataSourceAddress = firstPoint.Address;
                }
            }

            // 监听设备 ID 变化，响应式加载数据点
            EditingChannel.PropertyChanged += EditingChannel_PropertyChanged;

            IsEditMode = false;
            OnPropertyChanged(nameof(DrawerTitle));
            IsChannelEditorOpen = true;
        }

        [RelayCommand]
        private void EditChannel(MeasurementChannel? channel)
        {
            if (CurrentRecipe == null || channel == null)
            {
                Growl.Warning("请选择要编辑的通道");
                return;
            }

            ShowCacheToggle = false;

            // 克隆通道数据进行编辑
            EditingChannel = new MeasurementChannel
            {
                ChannelNumber = channel.ChannelNumber,
                ChannelName = channel.ChannelName,
                ChannelDescription = channel.ChannelDescription,
                IsEnabled = channel.IsEnabled,
                StandardValue = channel.StandardValue,
                UpperTolerance = channel.UpperTolerance,
                LowerTolerance = channel.LowerTolerance,
                ChannelType = channel.ChannelType,
                Unit = channel.Unit,
                DecimalPlaces = channel.DecimalPlaces,
                RequiresCalibration = channel.RequiresCalibration,
                StepNumber = channel.StepNumber,
                StepName = channel.StepName,
                PlcDeviceId = channel.PlcDeviceId,
                DataPointId = channel.DataPointId,
                DataSourceAddress = channel.DataSourceAddress,
                AvailableDataPoints = channel.AvailableDataPoints,
                UseCacheValue = channel.UseCacheValue
            };

            // 如果有设备ID，确保数据点列表已加载
            if (EditingChannel.PlcDeviceId != 0)
            {
                LoadDataPointsForChannel(EditingChannel);

                // 判断是否显示缓存开关
                var dp = EditingChannel.AvailableDataPoints.FirstOrDefault(d => d.PointId == EditingChannel.DataPointId);
                ShowCacheToggle = dp?.IsCacheGenerated == true
                    && !string.IsNullOrEmpty(dp.CacheFieldKey)
                    && IsCacheEnabledForDevice(EditingChannel.PlcDeviceId);

                if (!ShowCacheToggle)
                {
                    EditingChannel.UseCacheValue = false;
                }
            }

            // 监听设备 ID 变化，响应式加载数据点
            EditingChannel.PropertyChanged += EditingChannel_PropertyChanged;

            IsEditMode = true;
            OnPropertyChanged(nameof(DrawerTitle));
            IsChannelEditorOpen = true;
        }

        /// <summary>
        /// 监听编辑中通道的属性变化，实现响应式加载数据点
        /// </summary>
        private void EditingChannel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (EditingChannel == null) return;

            if (e.PropertyName == nameof(MeasurementChannel.PlcDeviceId))
            {
                // PLC 设备变化时，自动加载数据点
                if (EditingChannel.PlcDeviceId != 0)
                {
                    LoadDataPointsForChannel(EditingChannel);
                    // 清空原有数据点选择
                    EditingChannel.DataPointId = string.Empty;
                    EditingChannel.DataSourceAddress = string.Empty;
                    // 自动选中第一个数据点
                    if (EditingChannel.AvailableDataPoints.Any())
                    {
                        var firstPoint = EditingChannel.AvailableDataPoints.First();
                        EditingChannel.DataPointId = firstPoint.PointId;
                        EditingChannel.DataSourceAddress = firstPoint.Address;
                    }
                    _log.Info($"已为编辑通道加载 PLC 设备 {EditingChannel.PlcDeviceId} 的数据点");
                }
                else
                {
                    EditingChannel.AvailableDataPoints = new ObservableCollection<DataPoint>();
                    EditingChannel.DataPointId = string.Empty;
                    EditingChannel.DataSourceAddress = string.Empty;
                }
            }
            else if (e.PropertyName == nameof(MeasurementChannel.DataPointId))
            {
                // 数据点变化时，自动填充地址
                if (!string.IsNullOrEmpty(EditingChannel.DataPointId))
                {
                    var dataPoint = EditingChannel.AvailableDataPoints.FirstOrDefault(dp => dp.PointId == EditingChannel.DataPointId);
                    if (dataPoint != null)
                    {
                        EditingChannel.DataSourceAddress = dataPoint.Address;
                        _log.Info($"已设置通道数据点地址: {dataPoint.Address}");
                    }

                    // 判断是否显示"使用缓存值"开关：点位是缓存生成的 + 设备启用了缓存
                    ShowCacheToggle = dataPoint?.IsCacheGenerated == true
                        && !string.IsNullOrEmpty(dataPoint.CacheFieldKey)
                        && IsCacheEnabledForDevice(EditingChannel.PlcDeviceId);

                    // 如果缓存未启用，强制关闭 UseCacheValue
                    if (!ShowCacheToggle)
                    {
                        EditingChannel.UseCacheValue = false;
                    }
                }
                else
                {
                    ShowCacheToggle = false;
                    EditingChannel.UseCacheValue = false;
                }
            }
        }

        [RelayCommand]
        private void SaveChannel()
        {
            if (CurrentRecipe == null || EditingChannel == null)
            {
                return;
            }

            if (IsEditMode)
            {
                // 更新现有通道
                var originalChannel = CurrentRecipe.Channels.FirstOrDefault(c => c.ChannelNumber == EditingChannel.ChannelNumber);
                if (originalChannel != null)
                {
                    // 更新所有属性
                    originalChannel.ChannelName = EditingChannel.ChannelName;
                    originalChannel.ChannelDescription = EditingChannel.ChannelDescription;
                    originalChannel.IsEnabled = EditingChannel.IsEnabled;
                    originalChannel.StandardValue = EditingChannel.StandardValue;
                    originalChannel.UpperTolerance = EditingChannel.UpperTolerance;
                    originalChannel.LowerTolerance = EditingChannel.LowerTolerance;
                    originalChannel.ChannelType = EditingChannel.ChannelType;
                    originalChannel.Unit = EditingChannel.Unit;
                    originalChannel.DecimalPlaces = EditingChannel.DecimalPlaces;
                    originalChannel.RequiresCalibration = EditingChannel.RequiresCalibration;
                    originalChannel.StepNumber = EditingChannel.StepNumber;
                    originalChannel.StepName = EditingChannel.StepName;
                    if (!AvailablePlcDevices.Any())
                    {
                        originalChannel.PlcDeviceId = 0;
                        originalChannel.DataPointId = string.Empty;
                        originalChannel.DataSourceAddress = string.Empty;
                        originalChannel.AvailableDataPoints = new ObservableCollection<DataPoint>();
                    }
                    else
                    {
                        originalChannel.PlcDeviceId = EditingChannel.PlcDeviceId;
                        originalChannel.DataPointId = EditingChannel.DataPointId;
                        originalChannel.DataSourceAddress = EditingChannel.DataSourceAddress;
                        originalChannel.AvailableDataPoints = EditingChannel.AvailableDataPoints;
                        originalChannel.UseCacheValue = EditingChannel.UseCacheValue;
                    }

                    // 重新订阅属性变化事件（如果之前没订阅）
                    originalChannel.PropertyChanged -= Channel_PropertyChanged;
                    originalChannel.PropertyChanged += Channel_PropertyChanged;

                    Growl.Success("通道已更新");
                    _log.Info($"通道 {originalChannel.ChannelName} 已更新");
                }
            }
            else
            {
                // 添加新通道
                EditingChannel.PropertyChanged -= EditingChannel_PropertyChanged;
                EditingChannel.PropertyChanged += Channel_PropertyChanged;
                CurrentRecipe.Channels.Add(EditingChannel);
                SelectedChannel = EditingChannel;

                Growl.Success("已添加新通道");
                _log.Info($"已添加新通道: {EditingChannel.ChannelName}");
            }

            // 取消编辑事件监听
            if (EditingChannel != null)
            {
                EditingChannel.PropertyChanged -= EditingChannel_PropertyChanged;
            }

            IsChannelEditorOpen = false;

            // 触发 UI 刷新
            OnPropertyChanged(nameof(Channels));
        }

        [RelayCommand]
        private void CancelEditChannel()
        {
            // 取消事件监听
            if (EditingChannel != null)
            {
                EditingChannel.PropertyChanged -= EditingChannel_PropertyChanged;
            }

            IsChannelEditorOpen = false;
            EditingChannel = null;
        }

        [RelayCommand]
        private void DeleteChannel()
        {
            if (CurrentRecipe == null || SelectedChannel == null)
            {
                Growl.Warning("请选择要删除的通道");
                return;
            }
            var res = MessageBox.Show("你确定要删除该通道？", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res == MessageBoxResult.Yes)
            {
                // 取消订阅属性变化事件
                SelectedChannel.PropertyChanged -= Channel_PropertyChanged;
                var channelName = SelectedChannel.ChannelName;

                CurrentRecipe.Channels.Remove(SelectedChannel);
                OnPropertyChanged(nameof(Channels));
                Growl.Info("已删除通道");
            }
        }



        /// <summary>
        /// 重新编号
        /// </summary>
        [RelayCommand]
        public void RenumberPoints()
        {
            if (CurrentRecipe?.Channels != null && CurrentRecipe?.Channels.Count != 0)
            {
                for (int i = 0; i < CurrentRecipe?.Channels.Count; i++)
                {
                    CurrentRecipe.Channels[i].ChannelNumber = i + 1;
                }
            }
            else
            {
                Growl.Warning("请先添加通道");
            }
        }

        /// <summary>
        /// 导入产品图片
        /// </summary>
        [RelayCommand]
        private void ImportProductImage()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All Files (*.*)|*.*",
                Title = "选择产品图片"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                if (CurrentRecipe != null)
                {
                    CurrentRecipe.ProductImagePath = openFileDialog.FileName;
                    OnPropertyChanged(nameof(ProductImagePath));
                    _log.Info($"已设置产品图片: {openFileDialog.FileName}");
                }
            }
        }

        /// <summary>
        /// 在图片上右键点击位置添加标注点
        /// </summary>
        [RelayCommand]
        private void AddAnnotationAtPoint()
        {
            if (CurrentRecipe == null)
            {
                Growl.Warning("请先选择配方");
                return;
            }

            if (SelectedChannel == null)
            {
                Growl.Warning("请先在右侧列表中选中一个通道");
                return;
            }

            if (SelectedChannel.Annotation != null)
            {
                // 更新已有标注的位置
                SelectedChannel.Annotation.X = ClickX;
                SelectedChannel.Annotation.Y = ClickY;
                SelectedChannel.Annotation.Label = $"CH{SelectedChannel.ChannelNumber}";
                SelectedChannel.Annotation.ChannelName = SelectedChannel.ChannelName;
                Growl.Info($"已更新通道 {SelectedChannel.ChannelName} 的标注位置");
            }
            else
            {
                // 新增标注
                SelectedChannel.Annotation = new ChannelAnnotation
                {
                    X = ClickX,
                    Y = ClickY,
                    ChannelNumber = SelectedChannel.ChannelNumber,
                    StepNumber = SelectedChannel.StepNumber,
                    Label = $"CH{SelectedChannel.ChannelNumber}",
                    ChannelName = SelectedChannel.ChannelName
                };
                Growl.Success($"已为通道 {SelectedChannel.ChannelName} 添加标注 (工步{SelectedChannel.StepNumber})");
            }

            OnPropertyChanged(nameof(Annotations));
            _log.Info($"标注点已添加: 通道{SelectedChannel.ChannelNumber} 位置({ClickX:F0},{ClickY:F0})");
        }

        /// <summary>
        /// 删除选中通道的标注点
        /// </summary>
        [RelayCommand]
        private void DeleteAnnotation()
        {
            if (CurrentRecipe == null || SelectedChannel == null)
            {
                Growl.Warning("请先选中一个通道");
                return;
            }

            if (SelectedChannel.Annotation != null)
            {
                SelectedChannel.Annotation = null;
                OnPropertyChanged(nameof(Annotations));
                Growl.Info($"已删除通道 {SelectedChannel.ChannelName} 的标注");
            }
            else
            {
                Growl.Warning("该通道没有标注点");
            }
        }
    }
}