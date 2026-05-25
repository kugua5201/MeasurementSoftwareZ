using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Logs;
using MeasurementSoftware.Services.Measurements;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using HandyControl.Controls;
using MeasurementSoftware.Services.Config;
using MessageBox = HandyControl.Controls.MessageBox;

namespace MeasurementSoftware.ViewModels
{
    public partial class ChannelSettingViewModel : ObservableViewModel
    {
        private readonly ILogService _log;
        private readonly IRecipeConfigService _recipeConfigService;
        private readonly IDeviceConfigService _deviceConfigService;
        private readonly EnabledPlcDevicesObserver _enabledDevicesObserver;
        private readonly IReadOnlyDictionary<MeasurementChannelMode, IMeasurementChannelHandler> _channelHandlers;
        private readonly IMeasurementFormulaScriptEvaluator _formulaScriptEvaluator;

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
        /// 可用的 PLC 设备列表（仅包含已启用的设备）。
        /// 直接绑定共享的只读启用设备集合，避免各页面重复 new 列表。
        /// </summary>
        public ReadOnlyObservableCollection<PlcDevice> EnabledPlcDevices => _enabledDevicesObserver.EnabledDevicesView;

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

        private MeasurementChannelSourceBinding? selectedIndirectSourceBinding;
        private VirtualMeasurementChannelBinding? selectedVirtualSourceBinding;

        public MeasurementChannelSourceBinding? SelectedIndirectSourceBinding
        {
            get => selectedIndirectSourceBinding;
            set => SetProperty(ref selectedIndirectSourceBinding, value);
        }

        public VirtualMeasurementChannelBinding? SelectedVirtualSourceBinding
        {
            get => selectedVirtualSourceBinding;
            set => SetProperty(ref selectedVirtualSourceBinding, value);
        }

        /// <summary>
        /// 抽屉标题（根据是添加还是编辑动态显示）
        /// </summary>
        public string DrawerTitle => IsEditMode ? "编辑通道" : "添加通道";

        public IEnumerable<ChannelType> ChannelTypes => Enum.GetValues<ChannelType>();
        public IEnumerable<MeasurementFilterType> FilterTypes => Enum.GetValues<MeasurementFilterType>();
        public IEnumerable<MeasurementChannelMode> MeasurementChannelModes => Enum.GetValues<MeasurementChannelMode>();
        public IEnumerable<IndirectMeasurementTriggerMode> IndirectMeasurementTriggerModes => Enum.GetValues<IndirectMeasurementTriggerMode>();
        public IEnumerable<VirtualMeasurementSourceMode> VirtualMeasurementSourceModes => Enum.GetValues<VirtualMeasurementSourceMode>();
        public IEnumerable<VirtualMeasurementWaveformType> VirtualMeasurementWaveformTypes => Enum.GetValues<VirtualMeasurementWaveformType>();
        public IEnumerable<MeasurementChannel> AvailableVirtualSourceChannels => GetAvailableVirtualSourceChannels();

        partial void OnEditingChannelChanged(MeasurementChannel? value)
        {
            OnPropertyChanged(nameof(AvailableVirtualSourceChannels));
        }

        public ChannelSettingViewModel(ILogService log, IRecipeConfigService recipeConfigService, IDeviceConfigService deviceConfigService, IMeasurementFormulaScriptEvaluator formulaScriptEvaluator, IEnumerable<IMeasurementChannelHandler> channelHandlers)
        {
            _log = log;
            _recipeConfigService = recipeConfigService;
            _deviceConfigService = deviceConfigService;
            _formulaScriptEvaluator = formulaScriptEvaluator;
            _enabledDevicesObserver = new EnabledPlcDevicesObserver(_deviceConfigService);
            _channelHandlers = channelHandlers.ToDictionary(handler => handler.Mode);

            // 监听配方和设备变化
            if (_recipeConfigService is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(IRecipeConfigService.CurrentRecipe))
                    {
                        OnRecipeChanged();
                    }

                };
            }

            _enabledDevicesObserver.Changed += (_, _) =>
            {
                if (CurrentRecipe != null)
                {
                    foreach (var channel in CurrentRecipe.Channels)
                    {
                        LoadMeasurementBindingsForChannel(channel);
                        LoadResultOutputDataPointsForChannel(channel);
                    }
                }

                if (EditingChannel != null)
                {
                    LoadMeasurementBindingsForChannel(EditingChannel);
                    LoadResultOutputDataPointsForChannel(EditingChannel);
                }

            };

        }



        /// <summary>
        /// 配方切换时，刷新通道列表并为每个通道加载数据点
        /// </summary>
        private void OnRecipeChanged()
        {
            _enabledDevicesObserver.Rebind();
            if (CurrentRecipe != null)
            {
                foreach (var channel in CurrentRecipe.Channels)
                {
                    channel.PropertyChanged -= Channel_PropertyChanged;
                    channel.PropertyChanged += Channel_PropertyChanged;
                    LoadMeasurementBindingsForChannel(channel);

                    if (channel.ResultOutputPlcDeviceId != 0)
                    {
                        LoadResultOutputDataPointsForChannel(channel);
                    }
                }
            }
            OnPropertyChanged(nameof(CurrentRecipe));
            OnPropertyChanged(nameof(SelectedRecipe));
            OnPropertyChanged(nameof(Channels));
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
                Growl.Warning("配方保存失败");
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

        private IMeasurementChannelHandler GetChannelHandler(MeasurementChannel channel)
        {
            return _channelHandlers.TryGetValue(channel.MeasurementMode, out var handler)
                ? handler
                : _channelHandlers[MeasurementChannelMode.Direct];
        }

        private void LoadMeasurementBindingsForChannel(MeasurementChannel channel)
        {
            GetChannelHandler(channel).HydrateBindings(channel, _deviceConfigService);
        }

        private void SyncMeasurementBindingState(MeasurementChannel channel)
        {
            GetChannelHandler(channel).SyncBindings(channel, _deviceConfigService);
        }

        private bool ValidateMeasurementConfiguration(MeasurementChannel channel, out string errorMessage)
        {
            return GetChannelHandler(channel).ValidateConfiguration(channel, out errorMessage);
        }

        private void LoadDataPointsForChannel(MeasurementChannel channel)
        {
            var device = channel.RuntimeDevice;

            if (channel.PlcDeviceId == 0)
            {
                device = null;
            }
            else if (device == null || device.DeviceId != channel.PlcDeviceId)
            {
                device = _deviceConfigService.Devices.FirstOrDefault(d => d.DeviceId == channel.PlcDeviceId);
            }

            if (device != null && !device.IsEnabled)
            {
                channel.ClearRuntimeBindings();
                return;
            }

            if (!ReferenceEquals(channel.RuntimeDevice, device))
            {
                if (channel.PlcDeviceId == 0)
                {
                    channel.ClearRuntimeBindings();
                }
                else
                {
                    channel.HydrateRuntimeBindings(device);
                }
            }

            if (device == null)
            {
                return;
            }

            if (channel.RuntimeDataPoint == null || channel.RuntimeDataPoint.PointId != channel.DataPointId)
            {
                channel.HydrateRuntimeBindings(device);
            }
        }

        private void SyncChannelBindingState(MeasurementChannel channel)
        {
            if (channel.IsDirectMeasurementMode && channel.RuntimeDevice != null)
            {
                channel.PlcDeviceId = channel.RuntimeDevice.DeviceId;
            }

            if (channel.IsDirectMeasurementMode && channel.RuntimeDataPoint != null)
            {
                channel.DataPointId = channel.RuntimeDataPoint.PointId;
                channel.DataSourceAddress = channel.RuntimeDataPoint.Address;
            }
            else if (channel.IsDirectMeasurementMode && channel.PlcDeviceId == 0)
            {
                channel.DataPointId = string.Empty;
                channel.DataSourceAddress = string.Empty;
                channel.UseCacheValue = false;
            }
            else if (!channel.IsDirectMeasurementMode)
            {
                channel.PlcDeviceId = 0;
                channel.DataPointId = string.Empty;
                channel.DataSourceAddress = string.Empty;
                channel.UseCacheValue = false;
            }

            if (channel.IsDirectMeasurementMode && channel.PlcDeviceId != 0)
            {
                LoadDataPointsForChannel(channel);
            }

            if (channel.ResultOutputRuntimeDevice != null)
            {
                channel.ResultOutputPlcDeviceId = channel.ResultOutputRuntimeDevice.DeviceId;
            }

            if (channel.ResultOutputRuntimeDataPoint != null)
            {
                channel.ResultOutputDataPointId = channel.ResultOutputRuntimeDataPoint.PointId;
                channel.ResultOutputAddress = channel.ResultOutputRuntimeDataPoint.Address;
            }
            else if (channel.ResultOutputPlcDeviceId == 0)
            {
                channel.ResultOutputDataPointId = string.Empty;
                channel.ResultOutputAddress = string.Empty;
            }

            if (channel.ResultOutputPlcDeviceId != 0)
            {
                LoadResultOutputDataPointsForChannel(channel);
            }
        }

        private void LoadResultOutputDataPointsForChannel(MeasurementChannel channel)
        {
            var device = channel.ResultOutputRuntimeDevice;

            if (channel.ResultOutputPlcDeviceId == 0)
            {
                device = null;
            }
            else if (device == null || device.DeviceId != channel.ResultOutputPlcDeviceId)
            {
                device = _deviceConfigService.Devices.FirstOrDefault(d => d.DeviceId == channel.ResultOutputPlcDeviceId);
            }

            if (device != null && !device.IsEnabled)
            {
                channel.ClearResultOutputBindings();
                return;
            }

            if (!ReferenceEquals(channel.ResultOutputRuntimeDevice, device))
            {
                if (channel.ResultOutputPlcDeviceId == 0)
                {
                    channel.ClearResultOutputBindings();
                }
                else
                {
                    channel.HydrateResultOutputBindings(device);
                }
            }

            if (device == null)
            {
                return;
            }

            if (channel.ResultOutputRuntimeDataPoint == null || channel.ResultOutputRuntimeDataPoint.PointId != channel.ResultOutputDataPointId)
            {
                channel.HydrateResultOutputBindings(device);
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
                MeasurementMode = MeasurementChannelMode.Direct
            };

            GetChannelHandler(EditingChannel).InitializeNewChannel(EditingChannel, EnabledPlcDevices.ToList());
            LoadMeasurementBindingsForChannel(EditingChannel);

            if (EditingChannel.IsDirectMeasurementMode && EditingChannel.RuntimeDevice != null && EditingChannel.AvailableDataPoints.Any())
            {
                EditingChannel.RuntimeDataPoint ??= EditingChannel.AvailableDataPoints.First();
            }

            // 监听设备 ID 变化，响应式加载数据点
            EditingChannel.PropertyChanged += EditingChannel_PropertyChanged;
            RegisterIndirectBindingEvents(EditingChannel);
            RegisterVirtualBindingEvents(EditingChannel);

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
                MeasurementType = channel.MeasurementType,
                IsEnabled = channel.IsEnabled,
                StandardValue = channel.StandardValue,
                UpperTolerance = channel.UpperTolerance,
                LowerTolerance = channel.LowerTolerance,
                ChannelType = channel.ChannelType,
                MeasurementMode = channel.MeasurementMode,
                Unit = channel.Unit,
                DecimalPlaces = channel.DecimalPlaces,
                SampleCount = channel.SampleCount,
                EnableFilter = channel.EnableFilter,
                FilterType = channel.FilterType,
                FilterSampleCount = channel.FilterSampleCount,
                RequiresCalibration = channel.RequiresCalibration,
                StepNumber = channel.StepNumber,
                StepName = channel.StepName,
                PlcDeviceId = channel.PlcDeviceId,
                DataPointId = channel.DataPointId,
                DataSourceAddress = channel.DataSourceAddress,
                UseCacheValue = channel.UseCacheValue,
                EnableResultOutput = channel.EnableResultOutput,
                ResultOutputPlcDeviceId = channel.ResultOutputPlcDeviceId,
                ResultOutputDataPointId = channel.ResultOutputDataPointId,
                ResultOutputAddress = channel.ResultOutputAddress,
                ResultOutputOkValue = channel.ResultOutputOkValue,
                ResultOutputNgValue = channel.ResultOutputNgValue,
                IndirectFormula = channel.IndirectFormula,
                IndirectTriggerMode = channel.IndirectTriggerMode,
                VirtualSourceMode = channel.VirtualSourceMode,
                VirtualWaveformType = channel.VirtualWaveformType,
                VirtualWaveformAmplitude = channel.VirtualWaveformAmplitude,
                VirtualWaveformPeriodSeconds = channel.VirtualWaveformPeriodSeconds,
                VirtualWaveformDutyCycle = channel.VirtualWaveformDutyCycle,
                VirtualWaveformOffset = channel.VirtualWaveformOffset,
                VirtualFormula = channel.VirtualFormula
            };

            EditingChannel.ReplaceIndirectSourceBindings(channel.IndirectSourceBindings.Select(binding => binding.Clone()));
            EditingChannel.ReplaceVirtualSourceBindings(channel.VirtualSourceBindings.Select(binding => binding.Clone()));

            LoadMeasurementBindingsForChannel(EditingChannel);

            if (EditingChannel.IsDirectMeasurementMode)
            {
                var dp = EditingChannel.RuntimeDataPoint;
                ShowCacheToggle = dp?.IsCacheGenerated == true
                    && !string.IsNullOrEmpty(dp.CacheFieldKey)
                    && IsCacheEnabledForDevice(EditingChannel.RuntimeDevice?.DeviceId ?? 0);

                if (!ShowCacheToggle)
                {
                    EditingChannel.UseCacheValue = false;
                }
            }

            if (EditingChannel.ResultOutputPlcDeviceId != 0)
            {
                LoadResultOutputDataPointsForChannel(EditingChannel);
            }

            // 监听设备 ID 变化，响应式加载数据点
            EditingChannel.PropertyChanged += EditingChannel_PropertyChanged;
            RegisterIndirectBindingEvents(EditingChannel);
            RegisterVirtualBindingEvents(EditingChannel);

            IsEditMode = true;
            OnPropertyChanged(nameof(DrawerTitle));
            IsChannelEditorOpen = true;
        }

        private void RegisterVirtualBindingEvents(MeasurementChannel channel)
        {
            channel.VirtualSourceBindings.CollectionChanged -= VirtualSourceBindings_CollectionChanged;
            channel.VirtualSourceBindings.CollectionChanged += VirtualSourceBindings_CollectionChanged;

            foreach (var binding in channel.VirtualSourceBindings)
            {
                binding.PropertyChanged -= VirtualSourceBinding_PropertyChanged;
                binding.PropertyChanged += VirtualSourceBinding_PropertyChanged;
            }
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

            if (e.PropertyName == nameof(MeasurementChannel.MeasurementMode))
            {
                GetChannelHandler(channel).InitializeNewChannel(channel, EnabledPlcDevices.ToList());
                LoadMeasurementBindingsForChannel(channel);
                ShowCacheToggle = channel.IsDirectMeasurementMode
                    && channel.RuntimeDataPoint?.IsCacheGenerated == true
                    && !string.IsNullOrEmpty(channel.RuntimeDataPoint.CacheFieldKey)
                    && IsCacheEnabledForDevice(channel.RuntimeDevice?.DeviceId ?? 0);
                if (!ShowCacheToggle)
                {
                    channel.UseCacheValue = false;
                }
            }
            else if (e.PropertyName == nameof(MeasurementChannel.RuntimeDevice))
            {
                if (!channel.IsDirectMeasurementMode)
                {
                    return;
                }

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
                if (!channel.IsDirectMeasurementMode)
                {
                    return;
                }

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
            else if (e.PropertyName == nameof(MeasurementChannel.ResultOutputRuntimeDevice))
            {
                if (channel.ResultOutputRuntimeDevice != null)
                {
                    if (channel.ResultOutputRuntimeDataPoint == null && channel.AvailableResultOutputDataPoints.Any())
                    {
                        channel.ResultOutputRuntimeDataPoint = channel.AvailableResultOutputDataPoints.First();
                    }
                }
                else
                {
                    channel.ClearResultOutputBindings();
                }
            }
            else if (e.PropertyName == nameof(MeasurementChannel.ResultOutputRuntimeDataPoint))
            {
                if (channel.ResultOutputRuntimeDataPoint != null)
                {
                    _log.Info($"已设置通道结果输出地址: {channel.ResultOutputRuntimeDataPoint.Address}");
                }
            }
        }

        private void RegisterIndirectBindingEvents(MeasurementChannel channel)
        {
            channel.IndirectSourceBindings.CollectionChanged -= IndirectSourceBindings_CollectionChanged;
            channel.IndirectSourceBindings.CollectionChanged += IndirectSourceBindings_CollectionChanged;

            foreach (var binding in channel.IndirectSourceBindings)
            {
                binding.PropertyChanged -= IndirectSourceBinding_PropertyChanged;
                binding.PropertyChanged += IndirectSourceBinding_PropertyChanged;
            }
        }

        private void UnregisterIndirectBindingEvents(MeasurementChannel? channel)
        {
            if (channel == null)
            {
                return;
            }

            channel.IndirectSourceBindings.CollectionChanged -= IndirectSourceBindings_CollectionChanged;
            foreach (var binding in channel.IndirectSourceBindings)
            {
                binding.PropertyChanged -= IndirectSourceBinding_PropertyChanged;
            }

            channel.VirtualSourceBindings.CollectionChanged -= VirtualSourceBindings_CollectionChanged;
            foreach (var binding in channel.VirtualSourceBindings)
            {
                binding.PropertyChanged -= VirtualSourceBinding_PropertyChanged;
            }
        }

        private void IndirectSourceBindings_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (MeasurementChannelSourceBinding binding in e.NewItems)
                {
                    binding.PropertyChanged -= IndirectSourceBinding_PropertyChanged;
                    binding.PropertyChanged += IndirectSourceBinding_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (MeasurementChannelSourceBinding binding in e.OldItems)
                {
                    binding.PropertyChanged -= IndirectSourceBinding_PropertyChanged;
                }
            }
        }

        private void VirtualSourceBindings_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (VirtualMeasurementChannelBinding binding in e.NewItems)
                {
                    binding.PropertyChanged -= VirtualSourceBinding_PropertyChanged;
                    binding.PropertyChanged += VirtualSourceBinding_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (VirtualMeasurementChannelBinding binding in e.OldItems)
                {
                    binding.PropertyChanged -= VirtualSourceBinding_PropertyChanged;
                }
            }
        }

        private void VirtualSourceBinding_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not VirtualMeasurementChannelBinding binding)
            {
                return;
            }

            if (e.PropertyName == nameof(VirtualMeasurementChannelBinding.RuntimeChannel) && binding.RuntimeChannel == EditingChannel)
            {
                binding.RuntimeChannel = null;
            }

            if (e.PropertyName == nameof(VirtualMeasurementChannelBinding.RuntimeChannel)
                && binding.RuntimeChannel != null
                && WouldCreateVirtualCycle(EditingChannel, binding.RuntimeChannel))
            {
                var sourceChannelName = binding.RuntimeChannel.ChannelName;
                binding.RuntimeChannel = null;
                Growl.Warning($"来源通道 {sourceChannelName} 会形成循环引用，已自动取消选择");
                return;
            }
        }

        private void IndirectSourceBinding_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not MeasurementChannelSourceBinding binding)
            {
                return;
            }

            if (e.PropertyName == nameof(MeasurementChannelSourceBinding.RuntimeDevice))
            {
                if (binding.RuntimeDevice == null)
                {
                    binding.ClearRuntimeBindings();
                    return;
                }

                if (binding.RuntimeDataPoint == null && binding.AvailableDataPoints.Any())
                {
                    binding.RuntimeDataPoint = binding.AvailableDataPoints.First();
                }
            }
        }

        [RelayCommand]
        private void AddIndirectSourceBinding()
        {
            if (EditingChannel == null)
            {
                return;
            }

            var nextIndex = EditingChannel.IndirectSourceBindings.Count + 1;
            var binding = new MeasurementChannelSourceBinding
            {
                SourceKey = $"X{nextIndex}"
            };

            binding.PropertyChanged += IndirectSourceBinding_PropertyChanged;
            binding.RuntimeDevice = EnabledPlcDevices.FirstOrDefault();
            EditingChannel.IndirectSourceBindings.Add(binding);
            SelectedIndirectSourceBinding = binding;
        }

        [RelayCommand]
        private void RemoveIndirectSourceBinding(MeasurementChannelSourceBinding? binding)
        {
            if (EditingChannel == null || binding == null)
            {
                return;
            }

            if (EditingChannel.IndirectSourceBindings.Count <= 1)
            {
                Growl.Warning("间接测量至少需要保留一个数据源");
                return;
            }

            binding.PropertyChanged -= IndirectSourceBinding_PropertyChanged;
            EditingChannel.IndirectSourceBindings.Remove(binding);
            SelectedIndirectSourceBinding = EditingChannel.IndirectSourceBindings.FirstOrDefault();
        }

        [RelayCommand]
        private void CheckIndirectFormula()
        {
            if (EditingChannel == null)
            {
                return;
            }

            if (!EditingChannel.IsIndirectMeasurementMode)
            {
                Growl.Warning("当前不是间接测量模式");
                return;
            }

            if (!TryBuildIndirectFormulaVariables(EditingChannel, out var variables, out var errorMessage))
            {
                Growl.Warning(errorMessage);
                _log.Warn(errorMessage);
                return;
            }

            if (!_formulaScriptEvaluator.TryEvaluateScript(EditingChannel.IndirectFormula, variables, out var result, out var calculatedVariables, out var executionSteps, out errorMessage))
            {
                Growl.Warning($"脚本检查失败：{errorMessage}");
                _log.Warn($"脚本检查失败：{errorMessage}");
                return;
            }

            var intermediateVariableCount = Math.Max(0, calculatedVariables.Count - variables.Count - 1);
            //Growl.Success($"脚本检查通过，RESULT = {result:F6}，中间变量 {intermediateVariableCount} 个");
            Growl.Success($"脚本检查通过，中间变量 {intermediateVariableCount} 个");
            _log.Info($"脚本检查通过，中间变量 {intermediateVariableCount} 个");
        }

        [RelayCommand]
        private void AddVirtualSourceBinding()
        {
            if (EditingChannel == null)
            {
                return;
            }

            var nextIndex = EditingChannel.VirtualSourceBindings.Count + 1;
            var binding = new VirtualMeasurementChannelBinding
            {
                SourceKey = $"X{nextIndex}",
                RuntimeChannel = GetAvailableVirtualSourceChannels().FirstOrDefault()
            };

            binding.PropertyChanged += VirtualSourceBinding_PropertyChanged;
            EditingChannel.VirtualSourceBindings.Add(binding);
            SelectedVirtualSourceBinding = binding;
        }

        private IEnumerable<MeasurementChannel> GetAvailableVirtualSourceChannels()
        {
            if (EditingChannel == null)
            {
                return Channels;
            }

            return Channels.Where(channel => channel.IsEnabled && channel.ChannelNumber != EditingChannel.ChannelNumber && !WouldCreateVirtualCycle(EditingChannel, channel));
        }

        private bool WouldCreateVirtualCycle(MeasurementChannel? targetChannel, MeasurementChannel? candidateSourceChannel)
        {
            if (targetChannel == null || candidateSourceChannel == null)
            {
                return false;
            }

            if (candidateSourceChannel.ChannelNumber == targetChannel.ChannelNumber)
            {
                return true;
            }

            return DependsOnChannel(candidateSourceChannel, targetChannel.ChannelNumber, new HashSet<int>());
        }

        private static bool DependsOnChannel(MeasurementChannel channel, int targetChannelNumber, HashSet<int> visited)
        {
            if (!visited.Add(channel.ChannelNumber))
            {
                return false;
            }

            if (!channel.IsVirtualMeasurementMode || channel.VirtualSourceMode != VirtualMeasurementSourceMode.ChannelFormula)
            {
                return false;
            }

            foreach (var binding in channel.VirtualSourceBindings)
            {
                var sourceChannel = binding.RuntimeChannel;
                if (sourceChannel == null)
                {
                    continue;
                }

                if (sourceChannel.ChannelNumber == targetChannelNumber)
                {
                    return true;
                }

                if (DependsOnChannel(sourceChannel, targetChannelNumber, visited))
                {
                    return true;
                }
            }

            return false;
        }

        [RelayCommand]
        private void RemoveVirtualSourceBinding(VirtualMeasurementChannelBinding? binding)
        {
            if (EditingChannel == null || binding == null)
            {
                return;
            }

            if (EditingChannel.VirtualSourceBindings.Count <= 1)
            {
                Growl.Warning("虚拟测量至少需要保留一个来源通道");
                return;
            }

            binding.PropertyChanged -= VirtualSourceBinding_PropertyChanged;
            EditingChannel.VirtualSourceBindings.Remove(binding);
            SelectedVirtualSourceBinding = EditingChannel.VirtualSourceBindings.FirstOrDefault();
        }

        [RelayCommand]
        private void CheckVirtualFormula()
        {
            if (EditingChannel == null)
            {
                return;
            }

            if (!EditingChannel.IsVirtualMeasurementMode || EditingChannel.VirtualSourceMode != VirtualMeasurementSourceMode.ChannelFormula)
            {
                Growl.Warning("当前不是基于测量通道公式的虚拟测量模式");
                return;
            }

            var variables = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var binding in EditingChannel.VirtualSourceBindings)
            {
                var sourceKey = binding.SourceKey?.Trim();
                if (string.IsNullOrWhiteSpace(sourceKey))
                {
                    Growl.Warning("变量名不能为空");
                    return;
                }

                if (binding.RuntimeChannel == null)
                {
                    Growl.Warning($"变量 {sourceKey} 未绑定来源通道");
                    return;
                }

                if (!variables.TryAdd(sourceKey, 1d))
                {
                    Growl.Warning($"变量名 {sourceKey} 重复，请修改后重试");
                    return;
                }
            }

            if (!_formulaScriptEvaluator.TryEvaluateScript(EditingChannel.VirtualFormula, variables, out _, out var calculatedVariables, out _, out var errorMessage))
            {
                Growl.Warning($"脚本检查失败：{errorMessage}");
                _log.Warn($"虚拟测量脚本检查失败：{errorMessage}");
                return;
            }

            var intermediateVariableCount = Math.Max(0, calculatedVariables.Count - variables.Count - 1);
            Growl.Success($"脚本检查通过，中间变量 {intermediateVariableCount} 个");
            _log.Info($"虚拟测量脚本检查通过，中间变量 {intermediateVariableCount} 个");
        }

        [RelayCommand]
        private void SaveChannel()
        {
            if (CurrentRecipe == null || EditingChannel == null)
            {
                return;
            }

            if (EditingChannel.EnableFilter)
            {
                string filterSampleCountError = EditingChannel[nameof(MeasurementChannel.FilterSampleCount)];
                if (!string.IsNullOrWhiteSpace(filterSampleCountError))
                {
                    Growl.Warning($"滤波点数配置无效：{filterSampleCountError}");
                    return;
                }
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


            SyncMeasurementBindingState(EditingChannel);

            if (!ValidateMeasurementConfiguration(EditingChannel, out var errorMessage))
            {
                MessageBox.Show(errorMessage, "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
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
                    originalChannel.MeasurementMode = EditingChannel.MeasurementMode;
                    originalChannel.Unit = EditingChannel.Unit;
                    originalChannel.DecimalPlaces = EditingChannel.DecimalPlaces;
                    originalChannel.RequiresCalibration = EditingChannel.RequiresCalibration;
                    originalChannel.StepNumber = EditingChannel.StepNumber;
                    originalChannel.StepName = EditingChannel.StepName;
                    originalChannel.PlcDeviceId = EditingChannel.PlcDeviceId;
                    originalChannel.DataPointId = EditingChannel.DataPointId;
                    originalChannel.DataSourceAddress = EditingChannel.DataSourceAddress;
                    originalChannel.SampleCount = EditingChannel.SampleCount;
                    originalChannel.EnableFilter = EditingChannel.EnableFilter;
                    originalChannel.FilterType = EditingChannel.FilterType;
                    originalChannel.FilterSampleCount = EditingChannel.FilterSampleCount;
                    originalChannel.IndirectFormula = EditingChannel.IndirectFormula;
                    originalChannel.IndirectTriggerMode = EditingChannel.IndirectTriggerMode;
                    originalChannel.VirtualSourceMode = EditingChannel.VirtualSourceMode;
                    originalChannel.VirtualWaveformType = EditingChannel.VirtualWaveformType;
                    originalChannel.VirtualWaveformAmplitude = EditingChannel.VirtualWaveformAmplitude;
                    originalChannel.VirtualWaveformPeriodSeconds = EditingChannel.VirtualWaveformPeriodSeconds;
                    originalChannel.VirtualWaveformDutyCycle = EditingChannel.VirtualWaveformDutyCycle;
                    originalChannel.VirtualWaveformOffset = EditingChannel.VirtualWaveformOffset;
                    originalChannel.VirtualFormula = EditingChannel.VirtualFormula;
                     originalChannel.EnableResultOutput = EditingChannel.EnableResultOutput;
                     originalChannel.ResultOutputPlcDeviceId = EditingChannel.ResultOutputPlcDeviceId;
                     originalChannel.ResultOutputDataPointId = EditingChannel.ResultOutputDataPointId;
                     originalChannel.ResultOutputAddress = EditingChannel.ResultOutputAddress;
                     originalChannel.ResultOutputOkValue = EditingChannel.ResultOutputOkValue;
                     originalChannel.ResultOutputNgValue = EditingChannel.ResultOutputNgValue;
                    originalChannel.ReplaceIndirectSourceBindings(EditingChannel.IndirectSourceBindings.Select(binding => binding.Clone()));
                    originalChannel.ReplaceVirtualSourceBindings(EditingChannel.VirtualSourceBindings.Select(binding => binding.Clone()));
                  
                    if (EditingChannel.IsDirectMeasurementMode)
                    {
                        if (EditingChannel.PlcDeviceId == 0)
                        {
                            originalChannel.ClearRuntimeBindings();
                        }
                        else
                        {
                            LoadMeasurementBindingsForChannel(originalChannel);
                            originalChannel.UseCacheValue = EditingChannel.UseCacheValue;
                        }
                    }
                    else
                    {
                        originalChannel.ClearRuntimeBindings();
                        originalChannel.UseCacheValue = false;
                        LoadMeasurementBindingsForChannel(originalChannel);
                    }

                     if (!EditingChannel.EnableResultOutput || EditingChannel.ResultOutputPlcDeviceId == 0)
                     {
                         originalChannel.ClearResultOutputBindings();
                     }
                     else
                     {
                         LoadResultOutputDataPointsForChannel(originalChannel);
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
                UnregisterIndirectBindingEvents(EditingChannel);
            }

            IsChannelEditorOpen = false;

            // 触发 UI 刷新
            OnPropertyChanged(nameof(Channels));
        }

        private static bool TryBuildIndirectFormulaVariables(MeasurementChannel channel, out Dictionary<string, double> variables, out string errorMessage)
        {
            variables = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(channel.IndirectFormula))
            {
                errorMessage = "请先输入公式";
                return false;
            }

            foreach (var binding in channel.IndirectSourceBindings)
            {
                var sourceKey = binding.SourceKey?.Trim();
                if (string.IsNullOrWhiteSpace(sourceKey))
                {
                    errorMessage = "变量名不能为空";
                    return false;
                }

                if (!(char.IsLetter(sourceKey[0]) || sourceKey[0] == '_') || sourceKey.Any(ch => !char.IsLetterOrDigit(ch) && ch != '_'))
                {
                    errorMessage = $"变量名 {sourceKey} 只能包含字母、数字和下划线，且必须以字母或下划线开头";
                    return false;
                }

                if (!variables.TryAdd(sourceKey, 1d))
                {
                    errorMessage = $"变量名 {sourceKey} 重复，请修改后重试";
                    return false;
                }
            }

            return true;
        }

        [RelayCommand]
        private void CancelEditChannel()
        {
            // 取消事件监听
            if (EditingChannel != null)
            {
                EditingChannel.PropertyChanged -= EditingChannel_PropertyChanged;
                UnregisterIndirectBindingEvents(EditingChannel);
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
        /// 选中指定标注，并同步选中其所属通道。
        /// </summary>
        [RelayCommand]
        private void SelectAnnotation(ChannelAnnotation? annotation)
        {
            SelectedAnnotation = annotation;

            if (annotation == null || CurrentRecipe == null)
            {
                return;
            }

            var channel = CurrentRecipe.Channels.FirstOrDefault(c => ReferenceEquals(c.Annotation, annotation));
            if (channel != null)
            {
                SelectedChannel = channel;
            }
        }

        /// <summary>
        /// 删除标注点。
        /// 优先删除传入标注；如果未传入，则删除当前选中的标注。
        /// </summary>
        [RelayCommand]
        private void DeleteAnnotation(ChannelAnnotation? annotation)
        {
            if (CurrentRecipe == null)
            {
                Growl.Warning("请先选择一个配方");
                return;
            }

            annotation ??= SelectedAnnotation;
            if (annotation == null)
            {
                Growl.Warning("请先选中要删除的标注");
                return;
            }

            var channel = CurrentRecipe.Channels.FirstOrDefault(c => ReferenceEquals(c.Annotation, annotation));
            if (channel == null)
            {
                Growl.Warning("未找到该标注对应的通道");
                return;
            }

            var res = MessageBox.Show($"确定要删除通道 {channel.ChannelName} 的标注吗？", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res != MessageBoxResult.Yes)
            {
                return;
            }

            channel.Annotation = null;
            if (ReferenceEquals(SelectedAnnotation, annotation))
            {
                SelectedAnnotation = null;
            }

            SelectedChannel = channel;
            OnPropertyChanged(nameof(Annotations));
            Growl.Info($"已删除通道 {channel.ChannelName} 的标注");
        }
    }
}