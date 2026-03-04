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
            channel.AvailableDataPoints = new ObservableCollection<DataPoint>(dataPoints.Where(dp => dp.IsEnabled));
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
            if (e.PropertyName == nameof(PlcDevice.IsEnabled))
            {

                OnPropertyChanged(nameof(AvailablePlcDevices));
                //todo 如果关联设备被关闭了，选中通道是否不应该关闭？
                //if (!AvailablePlcDevices.Any())
                //{
                //    if (EditingChannel != null)
                //    {
                //        IsEditMode = true;
                //        SaveChannel();
                //    }
                //}
                OnPropertyChanged(nameof(EditingChannel.AvailableDataPoints));
                OnPropertyChanged(nameof(EditingChannel.DataPointId));

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

            // 创建新通道并打开编辑抽屉
            EditingChannel = new MeasurementChannel
            {
                ChannelNumber = CurrentRecipe.Channels.Count + 1,
                ChannelName = $"通道{CurrentRecipe.Channels.Count + 1}",
                IsEnabled = true,
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
                PlcDeviceId = channel.PlcDeviceId,
                DataPointId = channel.DataPointId,
                DataSourceAddress = channel.DataSourceAddress,
                AvailableDataPoints = channel.AvailableDataPoints
            };

            // 如果有设备ID，确保数据点列表已加载
            if (EditingChannel.PlcDeviceId != 0)
            {
                LoadDataPointsForChannel(EditingChannel);
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
    }
}