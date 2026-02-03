using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Logs;
using MeasurementSoftware.Services.Recipe;
using MeasurementSoftware.Services;
using System.Collections.ObjectModel;
using System.Windows;
using HandyControl.Controls;

namespace MeasurementSoftware.ViewModels
{
    public partial class ChannelSettingViewModel : ObservableViewModel
    {
        private readonly IRecipeService _recipeService;
        private readonly ILog _log;
        private readonly IRecipeConfigService _recipeConfigService;
        private readonly IDeviceConfigService _deviceConfigService;
        private readonly IUserSettingsService _userSettingsService;

        // 直接引用全局配置
        public MeasurementRecipe? CurrentRecipe => _recipeConfigService.CurrentRecipe;
        public MeasurementRecipe? SelectedRecipe => _recipeConfigService.CurrentRecipe;
        public ObservableCollection<MeasurementChannel> Channels => CurrentRecipe?.Channels ?? new ObservableCollection<MeasurementChannel>();

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

        public ChannelSettingViewModel(IRecipeService recipeService, ILog log,
            IRecipeConfigService recipeConfigService, IDeviceConfigService deviceConfigService, IUserSettingsService userSettingsService)
        {
            _recipeService = recipeService;
            _log = log;
            _recipeConfigService = recipeConfigService;
            _deviceConfigService = deviceConfigService;
            _userSettingsService = userSettingsService;

            // 订阅设备集合变化事件
            _deviceConfigService.Devices.CollectionChanged += Devices_CollectionChanged;

            // 订阅每个设备的属性变化事件
            foreach (var device in _deviceConfigService.Devices)
            {
                device.PropertyChanged += Device_PropertyChanged;
            }
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
                // 设备启用状态变化，刷新可用设备列表
                OnPropertyChanged(nameof(AvailablePlcDevices));
                _log.Info($"设备启用状态已更改，已刷新可用设备列表");
            }
        }

        [RelayCommand]
        private async Task OpenRecipeAsync()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "配方文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                    Title = "打开配方"
                };

                if (dialog.ShowDialog() == true)
                {
                    var recipe = await _recipeService.LoadRecipeFromFileAsync(dialog.FileName);
                    if (recipe != null)
                    {
                        // 保存到全局配置
                        _recipeConfigService.OpenRecipe(recipe, dialog.FileName);

                        // 保存到用户配置
                        _userSettingsService.Settings.LastRecipePath = dialog.FileName;
                        await _userSettingsService.SaveSettingsAsync();

                        // 为每个通道加载数据点列表
                        foreach (var channel in recipe.Channels)
                        {
                            channel.PropertyChanged += Channel_PropertyChanged;
                            if (!string.IsNullOrEmpty(channel.PlcDeviceId))
                            {
                                LoadDataPointsForChannel(channel);
                            }
                        }

                        Growl.Success($"配方 {recipe.RecipeName} 加载成功");
                        _log.Info($"配方 {recipe.RecipeName} 从 {dialog.FileName} 加载成功");

                        // 触发UI更新
                        OnPropertyChanged(nameof(CurrentRecipe));
                        OnPropertyChanged(nameof(SelectedRecipe));
                        OnPropertyChanged(nameof(Channels));
                    }
                    else
                    {
                        Growl.Error("配方加载失败");
                        _log.Error("配方加载失败");
                    }
                }
            }
            catch (Exception ex)
            {
                Growl.Error($"打开配方失败: {ex.Message}");
                _log.Error($"打开配方异常: {ex.Message}");
            }
        }

        private void Channel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MeasurementChannel.PlcDeviceId) && sender is MeasurementChannel channel)
            {
                // 当选择的PLC设备变化时，重新加载数据点列表
                LoadDataPointsForChannel(channel);

                // 清空当前选择的数据点
                channel.DataPointId = string.Empty;
                channel.DataSourceAddress = string.Empty;
            }
            else if (e.PropertyName == nameof(MeasurementChannel.DataPointId) && sender is MeasurementChannel channel2)
            {
                // 当选择数据点时，自动填充地址
                var dataPoint = channel2.AvailableDataPoints.FirstOrDefault(dp => dp.PointId == channel2.DataPointId);
                if (dataPoint != null)
                {
                    channel2.DataSourceAddress = dataPoint.Address;
                }
            }
        }

        private void LoadDataPointsForChannel(MeasurementChannel channel)
        {
            if (string.IsNullOrEmpty(channel.PlcDeviceId))
            {
                channel.AvailableDataPoints.Clear();
                return;
            }

            var dataPoints = _deviceConfigService.GetDataPointsByDeviceId(channel.PlcDeviceId);
            channel.AvailableDataPoints = new ObservableCollection<DataPoint>(dataPoints.Where(dp => dp.IsEnabled));
        }

        [RelayCommand]
        private void CreateNewRecipe()
        {
            var newRecipe = new MeasurementRecipe
            {
                RecipeId = Guid.NewGuid().ToString(),
                RecipeName = $"新配方_{DateTime.Now:yyyyMMddHHmmss}",
                CreateTime = DateTime.Now,
                ModifyTime = DateTime.Now
            };
            _recipeConfigService.OpenRecipe(newRecipe, string.Empty);
            Growl.Success("已创建新配方，请设置通道并保存");
            OnPropertyChanged(nameof(CurrentRecipe));
            OnPropertyChanged(nameof(SelectedRecipe));
            OnPropertyChanged(nameof(Channels));
        }

        [RelayCommand]
        private async Task SaveRecipeAsync()
        {
            if (CurrentRecipe == null)
            {
                Growl.Warning("没有配方需要保存");
                return;
            }

            try
            {
                // 如果已有路径，直接保存；否则显示另存为对话框
                if (!string.IsNullOrEmpty(_recipeConfigService.CurrentRecipePath))
                {
                    CurrentRecipe.ModifyTime = DateTime.Now;
                    var success = await _recipeService.SaveRecipeToFileAsync(CurrentRecipe, _recipeConfigService.CurrentRecipePath);
                    if (success)
                    {
                        Growl.Success("配方保存成功");
                        _log.Info($"配方 {CurrentRecipe.RecipeName} 保存成功");
                    }
                    else
                    {
                        Growl.Error("配方保存失败");
                        _log.Error($"配方 {CurrentRecipe.RecipeName} 保存失败");
                    }
                }
                else
                {
                    await SaveAsRecipeAsync();
                }
            }
            catch (Exception ex)
            {
                Growl.Error($"配方保存异常: {ex.Message}");
                _log.Error($"配方保存异常: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task SaveAsRecipeAsync()
        {
            if (CurrentRecipe == null)
            {
                Growl.Warning("没有配方需要保存");
                return;
            }

            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "配方文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                    Title = "另存为配方",
                    FileName = CurrentRecipe.RecipeName + ".json"
                };

                if (dialog.ShowDialog() == true)
                {
                    CurrentRecipe.ModifyTime = DateTime.Now;
                    var success = await _recipeService.SaveRecipeToFileAsync(CurrentRecipe, dialog.FileName);
                    if (success)
                    {
                        _recipeConfigService.UpdateRecipePath(dialog.FileName);
                        Growl.Success($"配方另存为 {dialog.FileName} 成功");
                        _log.Info($"配方 {CurrentRecipe.RecipeName} 另存为 {dialog.FileName} 成功");
                    }
                    else
                    {
                        Growl.Error("配方保存失败");
                        _log.Error($"配方 {CurrentRecipe.RecipeName} 保存失败");
                    }
                }
            }
            catch (Exception ex)
            {
                Growl.Error($"配方另存为异常: {ex.Message}");
                _log.Error($"配方另存为异常: {ex.Message}");
            }
        }

        [RelayCommand]
        private void DeleteRecipe()
        {
            if (CurrentRecipe == null)
            {
                HandyControl.Controls.Growl.Warning("没有配方可以关闭");
                return;
            }

            var result = System.Windows.MessageBox.Show($"确定要关闭配方 {CurrentRecipe.RecipeName} 吗？未保存的更改将丢失。", "确认关闭", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                var recipeName = CurrentRecipe.RecipeName;
                _recipeConfigService.CloseRecipe();
                HandyControl.Controls.Growl.Info($"已关闭配方 {recipeName}");
                _log.Info($"配方 {recipeName} 已关闭");
                OnPropertyChanged(nameof(CurrentRecipe));
                OnPropertyChanged(nameof(SelectedRecipe));
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
                PlcDeviceId = AvailablePlcDevices.FirstOrDefault()?.DeviceId ?? string.Empty,
            };

            // 加载数据点并设置默认值（如果有设备）
            if (!string.IsNullOrEmpty(EditingChannel.PlcDeviceId))
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
            if (!string.IsNullOrEmpty(EditingChannel.PlcDeviceId))
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
                if (!string.IsNullOrEmpty(EditingChannel.PlcDeviceId))
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
                    EditingChannel.AvailableDataPoints.Clear();
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

                    // 更新数据源相关属性
                    originalChannel.PlcDeviceId = EditingChannel.PlcDeviceId;
                    originalChannel.DataPointId = EditingChannel.DataPointId;
                    originalChannel.DataSourceAddress = EditingChannel.DataSourceAddress;
                    originalChannel.AvailableDataPoints = EditingChannel.AvailableDataPoints;

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

            // 取消订阅属性变化事件
            SelectedChannel.PropertyChanged -= Channel_PropertyChanged;
            var channelName = SelectedChannel.ChannelName;

            CurrentRecipe.Channels.Remove(SelectedChannel);
            OnPropertyChanged(nameof(Channels));
            Growl.Info("已删除通道");
        }
    }
}