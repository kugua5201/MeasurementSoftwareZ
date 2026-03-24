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
        public string? ProductImagePath => CurrentRecipe?.BasicInfo.ProductImagePath;

        /// <summary>
        /// 标注点集合（从通道中聚合）
        /// </summary>
        public IEnumerable<ChannelAnnotation> Annotations => CurrentRecipe?.Channels?.Where(c => c.Annotation != null).Select(c => c.Annotation!) ?? [];

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
        [ObservableProperty]
        private ObservableCollection<PlcDevice> availablePlcDevices;

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

                };
            }
            AvailablePlcDevices = new ObservableCollection<PlcDevice>(_deviceConfigService.Devices.Where(d => d.IsEnabled));

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
            AvailablePlcDevices = new ObservableCollection<PlcDevice>(_deviceConfigService.Devices.Where(d => d.IsEnabled));
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
                Growl.Warning("请先选择一个配方");
                return;
            }

            CurrentRecipe.BasicInfo.ModifyTime = DateTime.Now;
            var success = await _recipeConfigService.SaveCurrentRecipeAsync();
            if (success)
                Growl.Success("配方保存成功");
            else
                Growl.Warning(string.IsNullOrWhiteSpace(_recipeConfigService.LastSaveErrorMessage) ? "配方保存失败" : _recipeConfigService.LastSaveErrorMessage);
        }

        private void Channel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MeasurementChannel.RuntimeDevice) && sender is MeasurementChannel channel)
            {
                if (channel.RuntimeDevice == null)
                {
                    channel.BindDataPoint(null);
                    return;
                }

                if (channel.RuntimeDataPoint == null)
                {
                    channel.BindDataPoint(channel.AvailableDataPoints.FirstOrDefault(dp => dp.PointId == channel.DataPointId)
                        ?? channel.AvailableDataPoints.FirstOrDefault());
                }
            }
        }

        private void LoadDataPointsForChannel(MeasurementChannel channel)
        {
            var device = channel.RuntimeDevice;

            if (device == null && channel.PlcDeviceId != 0)
            {
                device = _deviceConfigService.Devices.FirstOrDefault(d => d.DeviceId == channel.PlcDeviceId);
            }

            if (!ReferenceEquals(channel.RuntimeDevice, device))
            {
                channel.BindDevice(device);
            }

            if (device == null)
            {
                channel.BindDataPoint(null);
                return;
            }

            channel.BindDataPoint(device.DataPoints.FirstOrDefault(dp => dp.PointId == channel.DataPointId));
        }

        private void SyncChannelBindingState(MeasurementChannel channel)
        {
            if (channel.RuntimeDevice != null)
            {
                channel.PlcDeviceId = channel.RuntimeDevice.DeviceId;
            }

            if (channel.RuntimeDataPoint != null)
            {
                channel.DataPointId = channel.RuntimeDataPoint.PointId;
                channel.DataSourceAddress = channel.RuntimeDataPoint.Address;
            }
            else if (channel.PlcDeviceId == 0)
            {
                channel.DataPointId = string.Empty;
                channel.DataSourceAddress = string.Empty;
                channel.UseCacheValue = false;
            }

            if (channel.PlcDeviceId != 0)
            {
                LoadDataPointsForChannel(channel);
            }
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

        [RelayCommand]
        private void AddChannel()
        {
            if (CurrentRecipe == null)
            {
                Growl.Warning("请先选择一个配方");
                return;
            }
            AvailablePlcDevices = new ObservableCollection<PlcDevice>(_deviceConfigService.Devices.Where(d => d.IsEnabled));
            ShowCacheToggle = false;

            // 创建新通道并打开编辑抽屉
            EditingChannel = new MeasurementChannel
            {
                ChannelNumber = CurrentRecipe.Channels.Count + 1,
                ChannelName = $"通道{CurrentRecipe.Channels.Count + 1}",
                MeasurementType = string.Empty,
                IsEnabled = true,
                RequiresCalibration = false,
                StandardValue = 0,
                UpperTolerance = 0.1,
                LowerTolerance = 0.1,
                ChannelType = ChannelTypes.FirstOrDefault(),
            };

            EditingChannel.BindDevice(AvailablePlcDevices.FirstOrDefault());

            // 加载数据点并设置默认值（如果有设备）
            if (EditingChannel.RuntimeDevice != null)
            {
                LoadDataPointsForChannel(EditingChannel);
                if (EditingChannel.AvailableDataPoints.Any())
                {
                    EditingChannel.RuntimeDataPoint = EditingChannel.AvailableDataPoints.First();
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
            AvailablePlcDevices = new ObservableCollection<PlcDevice>(_deviceConfigService.Devices.Where(d => d.IsEnabled));
            ShowCacheToggle = false;

            // 克隆通道数据进行编辑
            EditingChannel = new MeasurementChannel
            {
                ChannelNumber = channel.ChannelNumber,
                ChannelName = channel.ChannelName,
                ChannelDescription = channel.ChannelDescription,
                MeasurementType = channel.MeasurementType,
                IsEnabled = channel.IsEnabled,
                StandardValue = channel.StandardValue,
                UpperTolerance = channel.UpperTolerance,
                LowerTolerance = channel.LowerTolerance,
                ChannelType = channel.ChannelType,
                Unit = channel.Unit,
                DecimalPlaces = channel.DecimalPlaces,
                SampleCount = channel.SampleCount,
                RequiresCalibration = channel.RequiresCalibration,
                StepNumber = channel.StepNumber,
                StepName = channel.StepName,
                PlcDeviceId = channel.PlcDeviceId,
                DataPointId = channel.DataPointId,
                DataSourceAddress = channel.DataSourceAddress,
                UseCacheValue = channel.UseCacheValue
            };

            // 如果有设备ID，确保数据点列表已加载
            if (EditingChannel.PlcDeviceId != 0)
            {
                LoadDataPointsForChannel(EditingChannel);

                // 判断是否显示缓存开关
                var dp = EditingChannel.RuntimeDataPoint;
                ShowCacheToggle = dp?.IsCacheGenerated == true
                    && !string.IsNullOrEmpty(dp.CacheFieldKey)
                    && IsCacheEnabledForDevice(EditingChannel.RuntimeDevice?.DeviceId ?? 0);

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
            if (sender is not MeasurementChannel channel || EditingChannel == null || !ReferenceEquals(channel, EditingChannel))
            {
                return;
            }

            if (e.PropertyName == nameof(MeasurementChannel.RuntimeDevice))
            {
                if (channel.RuntimeDevice != null)
                {
                    if (channel.RuntimeDataPoint == null && channel.AvailableDataPoints.Any())
                    {
                        channel.RuntimeDataPoint = channel.AvailableDataPoints.First();
                    }

                    var device = channel.RuntimeDevice;
                    if (device != null)
                    {
                        _log.Info($"已为编辑通道加载 PLC 设备 {device.DeviceId} 的数据点");
                    }
                }
                else
                {
                    channel.ClearRuntimeBindings();
                }

                ShowCacheToggle = false;
            }
            else if (e.PropertyName == nameof(MeasurementChannel.RuntimeDataPoint))
            {
                if (channel.RuntimeDataPoint != null)
                {
                    var dataPoint = channel.RuntimeDataPoint;
                    _log.Info($"已设置通道数据点地址: {dataPoint.Address}");

                    ShowCacheToggle = dataPoint?.IsCacheGenerated == true
                        && !string.IsNullOrEmpty(dataPoint.CacheFieldKey)
                        && IsCacheEnabledForDevice(channel.RuntimeDevice?.DeviceId ?? 0);

                    if (!ShowCacheToggle)
                    {
                        channel.UseCacheValue = false;
                    }
                }
                else
                {
                    ShowCacheToggle = false;
                    channel.UseCacheValue = false;
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
            //如果启用工步测量，则需要检查已经启用的通道是否跟添加或者编辑的通道的工步练习，
            if (CurrentRecipe.OtherSettings.EnableStepMode)
            {
                if (EditingChannel.IsEnabled)
                {
                    var channels = CurrentRecipe.Channels.Where(c => c.ChannelNumber != EditingChannel.ChannelNumber && c.IsEnabled).Select(c => c.StepNumber).ToList();
                    int editChannelsStepNumber = EditingChannel.StepNumber;
                    channels.Add(editChannelsStepNumber);
                    var stepNumbers = channels.Distinct().OrderBy(n => n).ToList();
                    // 判断是否连续
                    bool isContinuous = stepNumbers.Zip(stepNumbers.Skip(1), (a, b) => b - a).All(diff => diff == 1);

                    if (!isContinuous)
                    {
                        MessageBox.Show("启用通道的工步号不连续，请检查所有已启用通道的工步号设置！", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
            }


            SyncChannelBindingState(EditingChannel);

            if (IsEditMode)
            {
                // 更新现有通道
                var originalChannel = CurrentRecipe.Channels.FirstOrDefault(c => c.ChannelNumber == EditingChannel.ChannelNumber);
                if (originalChannel != null)
                {
                    // 更新所有属性
                    originalChannel.ChannelName = EditingChannel.ChannelName;
                    originalChannel.ChannelDescription = EditingChannel.ChannelDescription;
                    originalChannel.MeasurementType = EditingChannel.MeasurementType;
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
                    originalChannel.PlcDeviceId = EditingChannel.PlcDeviceId;
                    originalChannel.DataPointId = EditingChannel.DataPointId;
                    originalChannel.DataSourceAddress = EditingChannel.DataSourceAddress;
                    originalChannel.UseCacheValue = EditingChannel.UseCacheValue;
                    originalChannel.SampleCount = EditingChannel.SampleCount;
                    if (EditingChannel.PlcDeviceId == 0)
                    {
                        originalChannel.ClearRuntimeBindings();
                    }
                    else
                    {
                        LoadDataPointsForChannel(originalChannel);
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
            if (CurrentRecipe == null)
            {
                Growl.Warning("请先选择一个配方");
                return;
            }
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All Files (*.*)|*.*",
                Title = "选择产品图片"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                if (CurrentRecipe != null)
                {
                    CurrentRecipe.BasicInfo.ProductImagePath = openFileDialog.FileName;
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