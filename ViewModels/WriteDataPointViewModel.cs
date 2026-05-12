using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Config;
using MeasurementSoftware.Services.Devices;
using MeasurementSoftware.Services.Logs;
using MeasurementSoftware.Services.WriteDataPoints;
using MultiProtocol.Model;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using MessageBox = HandyControl.Controls.MessageBox;

namespace MeasurementSoftware.ViewModels
{
    public partial class WriteDataPointViewModel : ObservableViewModel
    {
        private readonly ILogService _log;
        private readonly IRecipeConfigService _recipeConfigService;
        private readonly IDeviceConfigService _deviceConfigService;
        private readonly IPlcDeviceRuntimeService _plcDeviceRuntimeService;
        private readonly IWriteValueLabelRuleService _writeValueLabelRuleService;
        private readonly IWriteDataPointBindingService _writeDataPointBindingService;
        private readonly EnabledPlcDevicesObserver _enabledDevicesObserver;

        private ObservableCollection<WriteDataPointConfig>? _observedWriteDataPoints;
        private readonly ObservableCollection<WriteDataPointConfig> _enabledWriteDataPoints = [];
        private PropertyChangedEventHandler? _editingRuntimeDataPointPropertyChangedHandler;
        private WriteDataPointConfig? selectedWriteDataPoint;
        private WriteDataPointConfig? editingWriteDataPoint;
        private WriteValueDisplayRule? selectedDisplayRule;
        private bool isEditorOpen;
        private bool isEditMode;
        public MeasurementRecipe? CurrentRecipe => _recipeConfigService.CurrentRecipe;

        public bool HasRecipe => CurrentRecipe != null;

        public ObservableCollection<WriteDataPointConfig> WriteDataPoints => CurrentRecipe?.OtherSettings.WriteDataPoints ?? [];

        public ReadOnlyObservableCollection<WriteDataPointConfig> EnabledWriteDataPoints { get; }

        public ReadOnlyObservableCollection<PlcDevice> EnabledPlcDevices => _enabledDevicesObserver.EnabledDevicesView;

        public IEnumerable<WriteValueEditorMode> EditorModes => Enum.GetValues<WriteValueEditorMode>();

        public IEnumerable<WriteValueLabelDisplayMode> LabelDisplayModes => Enum.GetValues<WriteValueLabelDisplayMode>();

        public IEnumerable<FieldType> DataTypes => Enum.GetValues<FieldType>();

        public WriteDataPointConfig? SelectedWriteDataPoint
        {
            get => selectedWriteDataPoint;
            set
            {
                var previous = selectedWriteDataPoint;
                if (!SetProperty(ref selectedWriteDataPoint, value))
                {
                    return;
                }

                if (previous != null && !ReferenceEquals(previous, value))
                {
                    ResetRowEditingState(previous);
                }

                ResetRowEditingState(value);
                OnPropertyChanged(nameof(SelectedWriteDataPointCurrentValue));
            }
        }

        public WriteDataPointConfig? EditingWriteDataPoint
        {
            get => editingWriteDataPoint;
            set
            {
                var previous = editingWriteDataPoint;
                if (!SetProperty(ref editingWriteDataPoint, value))
                {
                    return;
                }

                if (previous != null)
                {
                    DetachEditingRuntimeDataPoint(previous);
                    previous.PropertyChanged -= EditingWriteDataPoint_PropertyChanged;
                    _writeDataPointBindingService.DetachAvailableDevices(previous);
                }

                if (value != null)
                {
                    _writeDataPointBindingService.AttachAvailableDevices(value, _enabledDevicesObserver.EnabledDevices);
                    _writeDataPointBindingService.HydrateRuntimeBindings(value, value.RuntimeDevice);
                    value.PropertyChanged -= EditingWriteDataPoint_PropertyChanged;
                    value.PropertyChanged += EditingWriteDataPoint_PropertyChanged;
                    AttachEditingRuntimeDataPoint(value);
                    value.SyncPendingWriteValueFromRuntime();
                    RefreshCurrentValueDisplayText(value);
                }

                SelectedDisplayRule = value?.DisplayRules.FirstOrDefault();
            }
        }

        public WriteValueDisplayRule? SelectedDisplayRule
        {
            get => selectedDisplayRule;
            set => SetProperty(ref selectedDisplayRule, value);
        }

        public bool IsEditorOpen
        {
            get => isEditorOpen;
            set => SetProperty(ref isEditorOpen, value);
        }

        public bool IsEditMode
        {
            get => isEditMode;
            set => SetProperty(ref isEditMode, value, () => OnPropertyChanged(nameof(DrawerTitle)));
        }

        public string DrawerTitle => IsEditMode ? "编辑点位" : "添加点位";

        public string SelectedWriteDataPointCurrentValue => SelectedWriteDataPoint?.RuntimeDataPoint?.CurrentValue?.ToString() ?? "--";

        public WriteDataPointViewModel(ILogService log, IRecipeConfigService recipeConfigService, IDeviceConfigService deviceConfigService, IPlcDeviceRuntimeService plcDeviceRuntimeService, IWriteValueLabelRuleService writeValueLabelRuleService, IWriteDataPointBindingService writeDataPointBindingService)
        {
            _log = log;
            _recipeConfigService = recipeConfigService;
            _deviceConfigService = deviceConfigService;
            _plcDeviceRuntimeService = plcDeviceRuntimeService;
            _writeValueLabelRuleService = writeValueLabelRuleService;
            _writeDataPointBindingService = writeDataPointBindingService;
            _enabledDevicesObserver = new EnabledPlcDevicesObserver(_deviceConfigService);
            EnabledWriteDataPoints = new ReadOnlyObservableCollection<WriteDataPointConfig>(_enabledWriteDataPoints);

            if (_recipeConfigService is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += RecipeConfigService_PropertyChanged;
            }

            _enabledDevicesObserver.Changed += EnabledDevicesObserver_Changed;
            _plcDeviceRuntimeService.DataPointsUpdated += PlcDeviceRuntimeService_DataPointsUpdated;

            _enabledDevicesObserver.Rebind();
            RebindWriteDataPointNotifications();
            HydrateWriteDataPoints();
            RefreshEnabledWriteDataPoints();
        }

        private void RecipeConfigService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(IRecipeConfigService.CurrentRecipe))
            {
                return;
            }

            _enabledDevicesObserver.Rebind();
            RebindWriteDataPointNotifications();
            HydrateWriteDataPoints();
          
            RefreshEnabledWriteDataPoints();
            SelectedWriteDataPoint = WriteDataPoints.FirstOrDefault();
            OnPropertyChanged(nameof(CurrentRecipe));
            OnPropertyChanged(nameof(HasRecipe));
            OnPropertyChanged(nameof(WriteDataPoints));
        }

        private void EnabledDevicesObserver_Changed(object? sender, EventArgs e)
        {
            HydrateWriteDataPoints();

            if (EditingWriteDataPoint != null)
            {
                _writeDataPointBindingService.AttachAvailableDevices(EditingWriteDataPoint, _enabledDevicesObserver.EnabledDevices);
                _writeDataPointBindingService.HydrateRuntimeBindings(EditingWriteDataPoint, EditingWriteDataPoint.RuntimeDevice);
                RefreshCurrentValueDisplayText(EditingWriteDataPoint);
            }
        }

        private void EditingWriteDataPoint_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not WriteDataPointConfig config || !ReferenceEquals(config, EditingWriteDataPoint))
            {
                return;
            }

            if (e.PropertyName == nameof(WriteDataPointConfig.RuntimeDevice))
            {
                _writeDataPointBindingService.BindRuntimeDevice(config, config.RuntimeDevice);
                AttachEditingRuntimeDataPoint(config);
                config.SyncPendingWriteValueFromRuntime();
                return;
            }

            if (e.PropertyName == nameof(WriteDataPointConfig.RuntimeDataPoint))
            {
                _writeDataPointBindingService.BindRuntimeDataPoint(config, config.RuntimeDataPoint);
                AttachEditingRuntimeDataPoint(config);
                config.SyncPendingWriteValueFromRuntime();
                return;
            }

            if (e.PropertyName is nameof(WriteDataPointConfig.EditorMode)
                or nameof(WriteDataPointConfig.LabelDisplayMode)
                or nameof(WriteDataPointConfig.DefaultDisplayText)
                or nameof(WriteDataPointConfig.PendingWriteValueText)
                or nameof(WriteDataPointConfig.WriteStatusText)
                or nameof(WriteDataPointConfig.RuleScriptText))
            {
                RefreshCurrentValueDisplayText(config);
            }
        }

        private void PlcDeviceRuntimeService_DataPointsUpdated(object? sender, PlcDataPointsUpdatedEventArgs e)
        {
            if (CurrentRecipe == null)
            {
                return;
            }

            var relatedConfigs = WriteDataPoints.Where(config => ReferenceEquals(config.RuntimeDevice, e.Device)).ToList();
            if (relatedConfigs.Count == 0)
            {
                return;
            }

            foreach (var config in relatedConfigs)
            {
                if (config.RuntimeDataPoint == null)
                {
                    continue;
                }

                var updatedPoint = e.DataPoints.FirstOrDefault(dp => ReferenceEquals(dp, config.RuntimeDataPoint) || dp.PointId == config.RuntimeDataPoint.PointId);
                if (updatedPoint == null)
                {
                    continue;
                }

                config.RuntimeDataPoint.CurrentValue = updatedPoint.CurrentValue;
                config.SyncPendingWriteValueFromRuntime();

                if (ReferenceEquals(config, SelectedWriteDataPoint))
                {
                    OnPropertyChanged(nameof(SelectedWriteDataPointCurrentValue));
                }
            }

            OnPropertyChanged(nameof(SelectedWriteDataPoint));
            OnPropertyChanged(nameof(WriteDataPoints));
        }

        private void AttachEditingRuntimeDataPoint(WriteDataPointConfig config)
        {
            DetachEditingRuntimeDataPoint(config);

            if (config.RuntimeDataPoint == null)
            {
                return;
            }

            _editingRuntimeDataPointPropertyChangedHandler ??= EditingRuntimeDataPoint_PropertyChanged;
            config.RuntimeDataPoint.PropertyChanged += _editingRuntimeDataPointPropertyChangedHandler;
        }

        private void DetachEditingRuntimeDataPoint(WriteDataPointConfig config)
        {
            if (_editingRuntimeDataPointPropertyChangedHandler == null || config.RuntimeDataPoint == null)
            {
                return;
            }

            config.RuntimeDataPoint.PropertyChanged -= _editingRuntimeDataPointPropertyChangedHandler;
        }

        private void EditingRuntimeDataPoint_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (EditingWriteDataPoint?.RuntimeDataPoint == null || !ReferenceEquals(sender, EditingWriteDataPoint.RuntimeDataPoint))
            {
                return;
            }

            if (e.PropertyName != nameof(DataPoint.CurrentValue))
            {
                return;
            }

            EditingWriteDataPoint.SyncPendingWriteValueFromRuntime();
            RefreshCurrentValueDisplayText(EditingWriteDataPoint);
        }

        private void RebindWriteDataPointNotifications()
        {
            if (_observedWriteDataPoints != null)
            {
                _observedWriteDataPoints.CollectionChanged -= WriteDataPoints_CollectionChanged;
                foreach (var config in _observedWriteDataPoints)
                {
                    config.PropertyChanged -= WriteDataPointConfig_PropertyChanged;
                    _writeDataPointBindingService.DetachAvailableDevices(config);
                }
            }

            _observedWriteDataPoints = CurrentRecipe?.OtherSettings.WriteDataPoints;
            if (_observedWriteDataPoints == null)
            {
                return;
            }

            _observedWriteDataPoints.CollectionChanged -= WriteDataPoints_CollectionChanged;
            _observedWriteDataPoints.CollectionChanged += WriteDataPoints_CollectionChanged;
            foreach (var config in _observedWriteDataPoints)
            {
                _writeDataPointBindingService.AttachAvailableDevices(config, _enabledDevicesObserver.EnabledDevices);
                config.PropertyChanged -= WriteDataPointConfig_PropertyChanged;
                config.PropertyChanged += WriteDataPointConfig_PropertyChanged;
            }

            RefreshEnabledWriteDataPoints();
        }

        private void WriteDataPoints_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (WriteDataPointConfig config in e.OldItems)
                {
                    config.PropertyChanged -= WriteDataPointConfig_PropertyChanged;
                    _writeDataPointBindingService.DetachAvailableDevices(config);
                }
            }

            if (e.NewItems != null)
            {
                foreach (WriteDataPointConfig config in e.NewItems)
                {
                    _writeDataPointBindingService.AttachAvailableDevices(config, _enabledDevicesObserver.EnabledDevices);
                    config.PropertyChanged -= WriteDataPointConfig_PropertyChanged;
                    config.PropertyChanged += WriteDataPointConfig_PropertyChanged;
                }
            }

   
            RefreshEnabledWriteDataPoints();
            OnPropertyChanged(nameof(WriteDataPoints));
        }

        private void WriteDataPointConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(WriteDataPointConfig.IsEnabled) or nameof(WriteDataPointConfig.DisplayName))
            {
                RefreshEnabledWriteDataPoints();
            }

            if (sender is not WriteDataPointConfig config)
            {
                return;
            }

            if (e.PropertyName is nameof(WriteDataPointConfig.EditorMode)
                or nameof(WriteDataPointConfig.LabelDisplayMode)
                or nameof(WriteDataPointConfig.DefaultDisplayText)
                or nameof(WriteDataPointConfig.PendingWriteValueText)
                or nameof(WriteDataPointConfig.WriteStatusText)
                or nameof(WriteDataPointConfig.RuleScriptText)
                or nameof(WriteDataPointConfig.RuntimeDataPoint))
            {
                RefreshCurrentValueDisplayText(config);
            }
        }

        private void HydrateWriteDataPoints()
        {
            CurrentRecipe?.OtherSettings.HydrateWriteDataPoints(_enabledDevicesObserver.EnabledDevices);

            foreach (var config in WriteDataPoints)
            {
                _writeDataPointBindingService.AttachAvailableDevices(config, _enabledDevicesObserver.EnabledDevices);
                _writeDataPointBindingService.HydrateRuntimeBindings(config, config.RuntimeDevice);
                EnsureRuleScriptInitialized(config);
                config.SyncPendingWriteValueFromRuntime();
                RefreshCurrentValueDisplayText(config);
            }

    
            RefreshEnabledWriteDataPoints();
        }

     
        private void RefreshEnabledWriteDataPoints()
        {
            var enabledItems = WriteDataPoints.Where(x => x.IsEnabled).ToList();

            _enabledWriteDataPoints.Clear();
            foreach (var enabledItem in enabledItems)
            {
                _enabledWriteDataPoints.Add(enabledItem);
            }

            if (SelectedWriteDataPoint != null && !SelectedWriteDataPoint.IsEnabled && _enabledWriteDataPoints.Count > 0)
            {
                SelectedWriteDataPoint = _enabledWriteDataPoints.FirstOrDefault();
            }

            OnPropertyChanged(nameof(EnabledWriteDataPoints));
        }

        [RelayCommand]
        private void AddWriteDataPoint()
        {
            if (CurrentRecipe == null)
            {
                Growl.Warning("请先选择一个配方");
                return;
            }

            var config = new WriteDataPointConfig
            {
                Index = GetNextWriteDataPointIndex(),
                DisplayName = $"写入点位{WriteDataPoints.Count + 1}",
                IsEnabled = true,
                EditorMode = WriteValueEditorMode.Label,
                LabelDisplayMode = WriteValueLabelDisplayMode.RawValue,
                DefaultDisplayText = "--"
            };

            _writeDataPointBindingService.AttachAvailableDevices(config, _enabledDevicesObserver.EnabledDevices);
            _writeDataPointBindingService.BindRuntimeDevice(config, EnabledPlcDevices.FirstOrDefault());
            _writeDataPointBindingService.BindRuntimeDataPoint(config, config.AvailableDataPoints.FirstOrDefault());
            config.PendingWriteValueText = config.RuntimeDataPoint?.CurrentValue?.ToString() ?? string.Empty;
            config.DisplayRules.Add(new WriteValueDisplayRule { SourceValue = "0", DisplayText = "OK" });
            config.DisplayRules.Add(new WriteValueDisplayRule { SourceValue = "1", DisplayText = "NG" });
            SyncRuleScriptFromDisplayRules(config);
            RefreshCurrentValueDisplayText(config);

            EditingWriteDataPoint = config;
            SelectedDisplayRule = config.DisplayRules.FirstOrDefault();
            IsEditMode = false;
            OnPropertyChanged(nameof(DrawerTitle));
            IsEditorOpen = true;
        }

        [RelayCommand]
        private void OpenWriteDataPointEditor(WriteDataPointConfig? config)
        {
            EditWriteDataPoint(config);
        }

        [RelayCommand]
        private void EditWriteDataPoint(WriteDataPointConfig? config)
        {
            config ??= SelectedWriteDataPoint;
            if (CurrentRecipe == null || config == null)
            {
                Growl.Warning("请选择要编辑的写入点位");
                return;
            }

            var editingConfig = CloneConfig(config);
            _writeDataPointBindingService.AttachAvailableDevices(editingConfig, _enabledDevicesObserver.EnabledDevices);
            _writeDataPointBindingService.HydrateRuntimeBindings(editingConfig, _enabledDevicesObserver.EnabledDevices.FirstOrDefault(d => d.DeviceId == editingConfig.PlcDeviceId));
            EnsureRuleScriptInitialized(editingConfig);
            RefreshCurrentValueDisplayText(editingConfig);

            EditingWriteDataPoint = editingConfig;
            SelectedDisplayRule = editingConfig.DisplayRules.FirstOrDefault();
            IsEditMode = true;
            OnPropertyChanged(nameof(DrawerTitle));
            IsEditorOpen = true;
        }

        [RelayCommand]
        private void CancelEdit()
        {
            IsEditorOpen = false;
            EditingWriteDataPoint = null;
            SelectedDisplayRule = null;
        }

        [RelayCommand]
        private void SaveWriteDataPoint()
        {
            if (CurrentRecipe == null || EditingWriteDataPoint == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(EditingWriteDataPoint.DisplayName))
            {
                Growl.Warning("请输入写入点位名称");
                return;
            }

            if (EditingWriteDataPoint.RuntimeDevice == null)
            {
                Growl.Warning("请选择设备");
                return;
            }

            if (EditingWriteDataPoint.RuntimeDataPoint == null)
            {
                Growl.Warning("请选择点位");
                return;
            }

            if (!ValidateRuleScript(EditingWriteDataPoint, out var ruleScriptErrorMessage))
            {
                Growl.Warning(string.IsNullOrWhiteSpace(ruleScriptErrorMessage) ? "规则脚本格式错误" : ruleScriptErrorMessage);
                return;
            }

            if (EditingWriteDataPoint.IsButtonMode)
            {
                if (string.IsNullOrWhiteSpace(EditingWriteDataPoint.ButtonDisplayText))
                {
                    Growl.Warning("按钮模式下按钮文本不能为空");
                    return;
                }

                if (string.IsNullOrWhiteSpace(EditingWriteDataPoint.ButtonWriteValueText))
                {
                    Growl.Warning("按钮模式下预设写入值不能为空");
                    return;
                }

                if (!WriteDataPointConfig.TryConvertWriteValue(EditingWriteDataPoint.ButtonWriteValueText, EditingWriteDataPoint.DataType, out _, out var presetValueErrorMessage))
                {
                    Growl.Warning(presetValueErrorMessage);
                    return;
                }
            }

            if (EditingWriteDataPoint.UsesRuleDisplay && !EditingWriteDataPoint.DisplayRules.Any())
            {
                Growl.Warning("启用规则显示后至少需要配置一条规则");
                return;
            }

            if (IsEditMode)
            {
                var original = WriteDataPoints.FirstOrDefault(x => x.Index == EditingWriteDataPoint.Index);
                if (original == null)
                {
                    Growl.Warning("未找到原始写入点位配置");
                    return;
                }

                ApplyConfigChanges(original, EditingWriteDataPoint);
                _writeDataPointBindingService.AttachAvailableDevices(original, _enabledDevicesObserver.EnabledDevices);
                _writeDataPointBindingService.HydrateRuntimeBindings(original, _enabledDevicesObserver.EnabledDevices.FirstOrDefault(d => d.DeviceId == original.PlcDeviceId));
                RefreshCurrentValueDisplayText(original);
                SelectedWriteDataPoint = original;
                Growl.Success("写入点位已更新");
            }
            else
            {
                var newConfig = CloneConfig(EditingWriteDataPoint);
                _writeDataPointBindingService.AttachAvailableDevices(newConfig, _enabledDevicesObserver.EnabledDevices);
                _writeDataPointBindingService.HydrateRuntimeBindings(newConfig, _enabledDevicesObserver.EnabledDevices.FirstOrDefault(d => d.DeviceId == newConfig.PlcDeviceId));
                newConfig.PendingWriteValueText = newConfig.RuntimeDataPoint?.CurrentValue?.ToString() ?? string.Empty;
                RefreshCurrentValueDisplayText(newConfig);
                WriteDataPoints.Add(newConfig);
                SelectedWriteDataPoint = newConfig;
                Growl.Success("已添加写入点位");
            }

            OnPropertyChanged(nameof(WriteDataPoints));
            IsEditorOpen = false;
            EditingWriteDataPoint = null;
            SelectedDisplayRule = null;
        }

        [RelayCommand]
        private void DeleteWriteDataPoint()
        {
            if (CurrentRecipe == null || SelectedWriteDataPoint == null)
            {
                Growl.Warning("请选择要删除的写入点位");
                return;
            }

            var result = MessageBox.Show($"确定删除写入点位“{SelectedWriteDataPoint.DisplayName}”吗？", "提示", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
            if (result != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            WriteDataPoints.Remove(SelectedWriteDataPoint);
            SelectedWriteDataPoint = WriteDataPoints.FirstOrDefault();
            Growl.Success("写入点位已删除");
        }

        [RelayCommand]
        private void ResetWriteDataPointOrder()
        {
            if (WriteDataPoints.Count <= 1)
            {
                return;
            }

            //WriteDataPoints的序号重1-最后显示
            WriteDataPoints.Select((x, i) => { x.Index = i + 1; return x; }).ToList();

            Growl.Success("写入点位顺序已重置");
        }

        [RelayCommand]
        private void AddDisplayRule()
        {
            if (EditingWriteDataPoint == null)
            {
                return;
            }

            var rule = new WriteValueDisplayRule();
            EditingWriteDataPoint.DisplayRules.Add(rule);
            SyncRuleScriptFromDisplayRules(EditingWriteDataPoint);
            RefreshCurrentValueDisplayText(EditingWriteDataPoint);
            SelectedDisplayRule = rule;
        }

        [RelayCommand]
        private void RemoveDisplayRule()
        {
            if (EditingWriteDataPoint == null || SelectedDisplayRule == null)
            {
                return;
            }

            EditingWriteDataPoint.DisplayRules.Remove(SelectedDisplayRule);
            SyncRuleScriptFromDisplayRules(EditingWriteDataPoint);
            RefreshCurrentValueDisplayText(EditingWriteDataPoint);
            SelectedDisplayRule = EditingWriteDataPoint.DisplayRules.FirstOrDefault();
        }

        [RelayCommand]
        private void BeginInlineEdit(WriteDataPointConfig? config)
        {
            config ??= SelectedWriteDataPoint;
            if (config == null || !config.IsTextBoxMode)
            {
                return;
            }

            BeginValueEditCore(config);
        }

        [RelayCommand]
        private void BeginDisplayValueEdit(WriteDataPointConfig? config)
        {
            config ??= SelectedWriteDataPoint;
            if (config == null)
            {
                return;
            }

            if (!ReferenceEquals(config, SelectedWriteDataPoint))
            {
                SelectedWriteDataPoint = config;
            }

            BeginValueEditCore(config);
        }

        public void BeginInlineEditFromView(WriteDataPointConfig? config)
        {
            BeginInlineEdit(config);
        }

        [RelayCommand]
        private void CancelInlineEdit()
        {
            CancelValueEditCore(SelectedWriteDataPoint);
        }

        [RelayCommand]
        private void CancelDisplayValueEdit()
        {
            CancelInlineEdit();
        }

        [RelayCommand]
        private void CancelRowValueEdit(WriteDataPointConfig? config)
        {
            CancelValueEditCore(config);
        }

        public void CancelInlineEditFromView()
        {
            CancelInlineEdit();
        }

        [RelayCommand]
        private async Task CommitInlineWriteAsync()
        {
            if (SelectedWriteDataPoint == null)
            {
                return;
            }

            await WriteValueAsync(SelectedWriteDataPoint, SelectedWriteDataPoint.EditingWriteValueText);
        }

        [RelayCommand]
        private async Task CommitDisplayValueEditAsync()
        {
            await CommitInlineWriteAsync();
        }

        [RelayCommand]
        private async Task CommitRowValueEditAsync(WriteDataPointConfig? config)
        {
            if (config == null)
            {
                return;
            }

            await WriteValueAsync(config, config.EditingWriteValueText);
        }

        public Task CommitInlineWriteFromViewAsync()
        {
            return CommitInlineWriteAsync();
        }

        [RelayCommand]
        private async Task SubmitButtonWriteAsync(WriteDataPointConfig? config)
        {
            config ??= SelectedWriteDataPoint;
            if (config == null)
            {
                return;
            }

            await WriteValueAsync(config, config.ButtonWriteValueText);
        }

        [RelayCommand]
        private async Task WriteRowValueAsync(WriteDataPointConfig? config)
        {
            if (config == null)
            {
                return;
            }

            if (config.IsTextBoxMode || config.IsButtonMode)
            {
                if (config.IsButtonMode)
                {
                    await WriteValueAsync(config, config.ButtonWriteValueText);
                    return;
                }

                if (!config.IsValueEditing)
                {
                    BeginValueEditCore(config);
                    return;
                }

                await WriteValueAsync(config, config.EditingWriteValueText);
                return;
            }

            await WriteValueAsync(config, config.PendingWriteValueText);
        }

        [RelayCommand]
        private async Task RefreshSelectedValueAsync()
        {
            if (SelectedWriteDataPoint == null || SelectedWriteDataPoint.RuntimeDevice == null || SelectedWriteDataPoint.RuntimeDataPoint == null)
            {
                Growl.Warning("请先选择有效的设备点位");
                return;
            }

            var value = await _plcDeviceRuntimeService.ReadDataPointValueAsync(SelectedWriteDataPoint.RuntimeDevice, SelectedWriteDataPoint.RuntimeDataPoint);
            SelectedWriteDataPoint.RuntimeDataPoint.CurrentValue = value;
            SelectedWriteDataPoint.SyncPendingWriteValueFromRuntime();
            RefreshCurrentValueDisplayText(SelectedWriteDataPoint);
            OnPropertyChanged(nameof(SelectedWriteDataPoint));
            OnPropertyChanged(nameof(SelectedWriteDataPointCurrentValue));
        }

        [RelayCommand]
        private async Task RefreshWriteDataPointValueAsync(WriteDataPointConfig? config)
        {
            if (config == null || config.RuntimeDevice == null || config.RuntimeDataPoint == null)
            {
                Growl.Warning("请先选择有效的设备点位");
                return;
            }

            var value = await _plcDeviceRuntimeService.ReadDataPointValueAsync(config.RuntimeDevice, config.RuntimeDataPoint);
            config.RuntimeDataPoint.CurrentValue = value;
            config.SyncPendingWriteValueFromRuntime();
            RefreshCurrentValueDisplayText(config);

            if (ReferenceEquals(config, SelectedWriteDataPoint))
            {
                OnPropertyChanged(nameof(SelectedWriteDataPointCurrentValue));
            }

            OnPropertyChanged(nameof(WriteDataPoints));
        }

        [RelayCommand]
        private async Task QuickWriteSelectedValueAsync()
        {
            if (SelectedWriteDataPoint == null)
            {
                Growl.Warning("请先选择一个写入点位");
                return;
            }

            await WriteValueAsync(SelectedWriteDataPoint, SelectedWriteDataPoint.IsValueEditing ? SelectedWriteDataPoint.EditingWriteValueText : SelectedWriteDataPoint.PendingWriteValueText);
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
            {
                Growl.Success("配方保存成功");
            }
            else
            {
                Growl.Warning("配方保存失败");
            }
        }

        private async Task WriteValueAsync(WriteDataPointConfig config, string rawValue)
        {
            if (config.RuntimeDevice == null || config.RuntimeDataPoint == null)
            {
                config.SetWriteStatus("未配置完整的设备和点位", false);
                Growl.Warning("当前写入点位未配置完整的设备和点位");
                return;
            }

            if (!config.IsEnabled)
            {
                config.SetWriteStatus("当前写入点位未启用，禁止写入", false);
                Growl.Warning("当前写入点位未启用，禁止写入");
                return;
            }

            if (!config.RuntimeDevice.IsEnabled || !config.RuntimeDataPoint.IsEnabled)
            {
                config.SetWriteStatus("设备或点位未启用，禁止写入", false);
                Growl.Warning("设备或点位未启用，禁止写入");
                return;
            }

            if (string.IsNullOrWhiteSpace(rawValue) && config.DataType != FieldType.String)
            {
                config.SetWriteStatus("请输入有效的写入值", false);
                Growl.Warning("请输入有效的写入值");
                return;
            }

            if (!WriteDataPointConfig.TryConvertWriteValue(rawValue, config.DataType, out var convertedValue, out var errorMessage))
            {
                config.SetWriteStatus(errorMessage, false);
                Growl.Warning(errorMessage);
                return;
            }

            var (success, message) = await _plcDeviceRuntimeService.WriteDataPointValueAsync(config.RuntimeDevice, config.RuntimeDataPoint, convertedValue!);
            if (!success)
            {
                config.SetWriteStatus(message ?? "写入失败", false);
                Growl.Warning(message ?? "写入失败");
                return;
            }

            config.RuntimeDataPoint.CurrentValue = convertedValue;
            config.CompleteValueEdit(convertedValue?.ToString() ?? string.Empty);
            config.SetWriteStatus(message ?? "写入成功", true);
            RefreshCurrentValueDisplayText(config);
            Growl.Success($"{config.DisplayName} 写入成功");
            _log.Info($"写入点位 {config.DisplayName} 成功，值：{convertedValue}");
            OnPropertyChanged(nameof(SelectedWriteDataPoint));
            OnPropertyChanged(nameof(SelectedWriteDataPointCurrentValue));
            OnPropertyChanged(nameof(WriteDataPoints));
        }

        private void BeginValueEditCore(WriteDataPointConfig config)
        {
            foreach (var item in WriteDataPoints.Where(item => !ReferenceEquals(item, config) && item.IsValueEditing))
            {
                item.CancelValueEdit();
            }

            if (!ReferenceEquals(config, SelectedWriteDataPoint))
            {
                SelectedWriteDataPoint = config;
            }

            if (!config.BeginValueEdit())
            {
                return;
            }

            config.ClearWriteStatus();
            RefreshCurrentValueDisplayText(config);
            OnPropertyChanged(nameof(WriteDataPoints));
        }

        private void CancelValueEditCore(WriteDataPointConfig? config)
        {
            if (config == null)
            {
                return;
            }

            config.CancelValueEdit();
            config.ClearWriteStatus();
            RefreshCurrentValueDisplayText(config);
            OnPropertyChanged(nameof(WriteDataPoints));
        }

        private void ResetRowEditingState(WriteDataPointConfig? config)
        {
            if (config == null)
            {
                return;
            }

            config.CancelValueEdit();
            config.SyncPendingWriteValueFromRuntime();
            RefreshCurrentValueDisplayText(config);
        }

        private WriteDataPointConfig CloneConfig(WriteDataPointConfig source)
        {
            var clone = new WriteDataPointConfig
            {
                Index = source.Index,
                IsEnabled = source.IsEnabled,
                DisplayName = source.DisplayName,
                PlcDeviceId = source.PlcDeviceId,
                DataPointId = source.DataPointId,
                DataType = source.DataType,
                Unit = source.Unit,
                Description = source.Description,
                EditorMode = source.EditorMode,
                LabelDisplayMode = source.LabelDisplayMode,
                DefaultDisplayText = source.DefaultDisplayText,
                PendingWriteValueText = source.PendingWriteValueText,
                ButtonWriteValueText = source.ButtonWriteValueText,
                ButtonDisplayText = source.ButtonDisplayText,
                RuleScriptText = source.RuleScriptText
            };

            foreach (var rule in source.DisplayRules)
            {
                clone.DisplayRules.Add(new WriteValueDisplayRule
                {
                    SourceValue = rule.SourceValue,
                    DisplayText = rule.DisplayText
                });
            }

            EnsureRuleScriptInitialized(clone);
            RefreshCurrentValueDisplayText(clone);

            return clone;
        }

        private void ApplyConfigChanges(WriteDataPointConfig target, WriteDataPointConfig source)
        {
            target.Index = source.Index;
            target.IsEnabled = source.IsEnabled;
            target.DisplayName = source.DisplayName;
            target.PlcDeviceId = source.PlcDeviceId;
            target.DataPointId = source.DataPointId;
            target.DataType = source.DataType;
            target.Unit = source.Unit;
            target.Description = source.Description;
            target.EditorMode = source.EditorMode;
            target.LabelDisplayMode = source.LabelDisplayMode;
            target.DefaultDisplayText = source.DefaultDisplayText;
            target.ButtonWriteValueText = source.ButtonWriteValueText;
            target.ButtonDisplayText = source.ButtonDisplayText;
            target.RuleScriptText = source.RuleScriptText;

            target.DisplayRules.Clear();
            foreach (var rule in source.DisplayRules)
            {
                target.DisplayRules.Add(new WriteValueDisplayRule
                {
                    SourceValue = rule.SourceValue,
                    DisplayText = rule.DisplayText
                });
            }

            EnsureRuleScriptInitialized(target);
            RefreshCurrentValueDisplayText(target);
        }

        private void EnsureRuleScriptInitialized(WriteDataPointConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.RuleScriptText))
            {
                SyncRuleScriptFromDisplayRules(config);
                config.SetRuleScriptValidationState(true, string.Empty);
                return;
            }

            ApplyParsedRuleScript(config, config.RuleScriptText);
        }

        private bool ValidateRuleScript(WriteDataPointConfig config, out string errorMessage)
        {
            if (!config.UsesRuleDisplay)
            {
                config.SetRuleScriptValidationState(true, string.Empty);
                RefreshCurrentValueDisplayText(config);
                errorMessage = string.Empty;
                return true;
            }

            var success = ApplyParsedRuleScript(config, config.RuleScriptText);
            errorMessage = config.RuleScriptStatusText;
            return success;
        }

        private void SyncRuleScriptFromDisplayRules(WriteDataPointConfig config)
        {
            config.RuleScriptText = _writeValueLabelRuleService.BuildRuleScript(config.DisplayRules, config.DefaultDisplayText);
            config.SetRuleScriptValidationState(true, string.Empty);
        }

        private bool ApplyParsedRuleScript(WriteDataPointConfig config, string? ruleScriptText)
        {
            var parseResult = _writeValueLabelRuleService.ParseRuleScript(ruleScriptText, config.DefaultDisplayText);
            if (!parseResult.IsValid)
            {
                config.SetRuleScriptValidationState(false, parseResult.StatusText);
                RefreshCurrentValueDisplayText(config);
                return false;
            }

            config.DisplayRules.Clear();
            foreach (var rule in parseResult.Rules)
            {
                config.DisplayRules.Add(new WriteValueDisplayRule
                {
                    SourceValue = rule.SourceValue,
                    DisplayText = rule.DisplayText
                });
            }

            config.DefaultDisplayText = parseResult.DefaultDisplayText;
            config.SetRuleScriptValidationState(true, string.Empty);
            RefreshCurrentValueDisplayText(config);
            return true;
        }

        private void RefreshCurrentValueDisplayText(WriteDataPointConfig config)
        {
            config.CurrentValueDisplayText = _writeValueLabelRuleService.GetDisplayText(
                config.RuntimeDataPoint?.CurrentValue,
                config.UsesRuleDisplay,
                config.DisplayRules,
                config.DefaultDisplayText);
        }

        private int GetNextWriteDataPointIndex()
        {
            return WriteDataPoints.Count == 0 ? 1 : WriteDataPoints.Max(x => x.Index) + 1;
        }
    }
}