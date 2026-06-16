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
        private readonly IWriteRestrictionScriptEvaluator _writeRestrictionScriptEvaluator;
        private readonly EnabledPlcDevicesObserver _enabledDevicesObserver;

        private ObservableCollection<WriteDataPointConfig>? _observedWriteDataPoints;

        private PropertyChangedEventHandler? _editingRuntimeDataPointPropertyChangedHandler;
        private WriteDataPointConfig? selectedWriteDataPoint;
        private WriteDataPointConfig? editingWriteDataPoint;
        private WriteValueDisplayRule? selectedDisplayRule;
        private WriteValueRestrictionVariable? selectedRestrictionVariable;
        private bool isEditorOpen;
        private bool isEditMode;
        public MeasurementRecipe? CurrentRecipe => _recipeConfigService.CurrentRecipe;

        public bool HasRecipe => CurrentRecipe != null;

        public ObservableCollection<WriteDataPointConfig> WriteDataPoints => CurrentRecipe?.OtherSettings.WriteDataPoints ?? [];


        public ObservableCollection<WriteDataPointConfig> EnabledWriteDataPoints { get; set; } = [];

        public ReadOnlyObservableCollection<PlcDevice> EnabledPlcDevices => _enabledDevicesObserver.EnabledDevicesView;

        public IEnumerable<WriteValueEditorMode> EditorModes => Enum.GetValues<WriteValueEditorMode>();

        public IEnumerable<WriteValueLabelDisplayMode> LabelDisplayModes => Enum.GetValues<WriteValueLabelDisplayMode>();

        public IEnumerable<WriteValueButtonInteractionMode> ButtonInteractionModes => Enum.GetValues<WriteValueButtonInteractionMode>();

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
                    SelectedRestrictionVariable = null;
                }

                if (value != null)
                {
                    _writeDataPointBindingService.AttachAvailableDevices(value, _enabledDevicesObserver.EnabledDevices);
                    _writeDataPointBindingService.HydrateRuntimeBindings(value, value.RuntimeDevice);
                    AttachRestrictionVariables(value);
                    value.PropertyChanged -= EditingWriteDataPoint_PropertyChanged;
                    value.PropertyChanged += EditingWriteDataPoint_PropertyChanged;
                    AttachEditingRuntimeDataPoint(value);
                    value.SyncPendingWriteValueFromRuntime();
                    RefreshCurrentValueDisplayText(value);
                }

                SelectedDisplayRule = value?.DisplayRules.FirstOrDefault();
                SelectedRestrictionVariable = value?.RestrictionVariables.FirstOrDefault();
            }
        }

        public WriteValueDisplayRule? SelectedDisplayRule
        {
            get => selectedDisplayRule;
            set => SetProperty(ref selectedDisplayRule, value);
        }

        public WriteValueRestrictionVariable? SelectedRestrictionVariable
        {
            get => selectedRestrictionVariable;
            set => SetProperty(ref selectedRestrictionVariable, value);
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

        public WriteDataPointViewModel(ILogService log, IRecipeConfigService recipeConfigService, IDeviceConfigService deviceConfigService, IPlcDeviceRuntimeService plcDeviceRuntimeService, IWriteValueLabelRuleService writeValueLabelRuleService, IWriteDataPointBindingService writeDataPointBindingService, IWriteRestrictionScriptEvaluator writeRestrictionScriptEvaluator)
        {
            _log = log;
            _recipeConfigService = recipeConfigService;
            _deviceConfigService = deviceConfigService;
            _plcDeviceRuntimeService = plcDeviceRuntimeService;
            _writeValueLabelRuleService = writeValueLabelRuleService;
            _writeDataPointBindingService = writeDataPointBindingService;
            _writeRestrictionScriptEvaluator = writeRestrictionScriptEvaluator;
            _enabledDevicesObserver = new EnabledPlcDevicesObserver(_deviceConfigService);

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
                AttachRestrictionVariables(EditingWriteDataPoint);
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
                or nameof(WriteDataPointConfig.ButtonInteractionMode)
                or nameof(WriteDataPointConfig.WriteStatusText)
                or nameof(WriteDataPointConfig.RuleScriptText)
                or nameof(WriteDataPointConfig.EnableWriteRestriction)
                or nameof(WriteDataPointConfig.WriteRestrictionScriptText))
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

            var relatedConfigs = WriteDataPoints.Where(config => ReferenceEquals(config.RuntimeDevice, e.Device)
                || config.RestrictionVariables.Any(variable => ReferenceEquals(variable.RuntimeDevice, e.Device))).ToList();
            if (relatedConfigs.Count == 0)
            {
                return;
            }

            foreach (var config in relatedConfigs)
            {
                if (config.RuntimeDataPoint != null)
                {
                    var updatedPoint = e.DataPoints.FirstOrDefault(dp => ReferenceEquals(dp, config.RuntimeDataPoint) || dp.PointId == config.RuntimeDataPoint.PointId);
                    if (updatedPoint != null)
                    {
                        config.RuntimeDataPoint.CurrentValue = updatedPoint.CurrentValue;
                        config.SyncPendingWriteValueFromRuntime();
                    }
                }

                foreach (var variable in config.RestrictionVariables.Where(variable => ReferenceEquals(variable.RuntimeDevice, e.Device) && variable.RuntimeDataPoint != null))
                {
                    var runtimeDataPoint = variable.RuntimeDataPoint;
                    if (runtimeDataPoint == null)
                    {
                        continue;
                    }

                    var updatedVariablePoint = e.DataPoints.FirstOrDefault(dp => ReferenceEquals(dp, runtimeDataPoint) || dp.PointId == runtimeDataPoint.PointId);
                    if (updatedVariablePoint != null)
                    {
                        runtimeDataPoint.CurrentValue = updatedVariablePoint.CurrentValue;
                    }
                }

                RefreshCurrentValueDisplayText(config);

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

        private void AttachRestrictionVariables(WriteDataPointConfig config)
        {
            for (int i = 0; i < config.RestrictionVariables.Count; i++)
            {
                var variable = config.RestrictionVariables[i];
                if (string.IsNullOrWhiteSpace(variable.VariableName))
                {
                    variable.VariableName = $"V{i + 1}";
                }

                variable.AvailableDevicesSource = _enabledDevicesObserver.EnabledDevices;
            }
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
                AttachRestrictionVariables(config);
                EnsureRuleScriptInitialized(config);
                config.SyncPendingWriteValueFromRuntime();
                RefreshCurrentValueDisplayText(config);
            }


            RefreshEnabledWriteDataPoints();
        }


        private void RefreshEnabledWriteDataPoints()
        {
            var enabledItems = WriteDataPoints.Where(x => x.IsEnabled).ToList();

            EnabledWriteDataPoints.Clear();
            foreach (var enabledItem in enabledItems)
            {
                EnabledWriteDataPoints.Add(enabledItem);
            }

            if (SelectedWriteDataPoint != null && !SelectedWriteDataPoint.IsEnabled && EnabledWriteDataPoints.Count > 0)
            {
                SelectedWriteDataPoint = EnabledWriteDataPoints.FirstOrDefault();
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

                if (EditingWriteDataPoint.IsButtonPressAndReleaseMode)
                {
                    if (string.IsNullOrWhiteSpace(EditingWriteDataPoint.ButtonReleaseWriteValueText))
                    {
                        Growl.Warning("按下/松开模式下松开写入值不能为空");
                        return;
                    }

                    if (!WriteDataPointConfig.TryConvertWriteValue(EditingWriteDataPoint.ButtonReleaseWriteValueText, EditingWriteDataPoint.DataType, out _, out var releaseValueErrorMessage))
                    {
                        Growl.Warning(releaseValueErrorMessage);
                        return;
                    }
                }
            }

            if (EditingWriteDataPoint.UsesRuleDisplay && !EditingWriteDataPoint.DisplayRules.Any())
            {
                Growl.Warning("启用规则显示后至少需要配置一条规则");
                return;
            }

            if (!ValidateWriteRestriction(EditingWriteDataPoint, out var writeRestrictionErrorMessage))
            {
                Growl.Warning(string.IsNullOrWhiteSpace(writeRestrictionErrorMessage) ? "限制模式配置无效" : writeRestrictionErrorMessage);
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
        private void AddRestrictionVariable()
        {
            if (EditingWriteDataPoint == null)
            {
                return;
            }

            var variable = new WriteValueRestrictionVariable
            {
                VariableName = $"V{EditingWriteDataPoint.RestrictionVariables.Count + 1}"
            };
            variable.AvailableDevicesSource = _enabledDevicesObserver.EnabledDevices;
            EditingWriteDataPoint.RestrictionVariables.Add(variable);
            SelectedRestrictionVariable = variable;
            RefreshCurrentValueDisplayText(EditingWriteDataPoint);
        }

        [RelayCommand]
        private void RemoveRestrictionVariable()
        {
            if (EditingWriteDataPoint == null || SelectedRestrictionVariable == null)
            {
                return;
            }

            EditingWriteDataPoint.RestrictionVariables.Remove(SelectedRestrictionVariable);
            SelectedRestrictionVariable = EditingWriteDataPoint.RestrictionVariables.FirstOrDefault();
            RefreshCurrentValueDisplayText(EditingWriteDataPoint);
        }

        [RelayCommand]
        private void CheckRestrictionVariables()
        {
            if (EditingWriteDataPoint == null)
            {
                return;
            }

            if (!EditingWriteDataPoint.UsesWriteRestriction)
            {
                Growl.Info("当前写入点位未启用限制模式");
                return;
            }

            if (!TryBuildRestrictionVariables(EditingWriteDataPoint, allowMissingCurrentValue: false, out var variables, out var variableErrorMessage))
            {
                EditingWriteDataPoint.SetWriteRestrictionValidationState(false, variableErrorMessage);
                EditingWriteDataPoint.SetWriteRestrictionEvaluationState(false, variableErrorMessage);
                Growl.Warning(variableErrorMessage);
                return;
            }

            if (!_writeRestrictionScriptEvaluator.TryEvaluateScript(EditingWriteDataPoint.WriteRestrictionScriptText, variables, out var isSatisfied, out var expressionErrorMessage))
            {
                EditingWriteDataPoint.SetWriteRestrictionValidationState(false, expressionErrorMessage);
                EditingWriteDataPoint.SetWriteRestrictionEvaluationState(false, expressionErrorMessage);
                Growl.Warning(expressionErrorMessage);
                return;
            }

            EditingWriteDataPoint.SetWriteRestrictionValidationState(true, string.Empty);
            EditingWriteDataPoint.SetWriteRestrictionEvaluationState(isSatisfied, isSatisfied ? string.Empty : "限制条件未满足，当前禁止写入和点击操作");
            Growl.Success(isSatisfied ? "脚本检查通过，当前条件已满足" : "脚本检查完成，但当前条件未满足");
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
            if (config == null || !config.IsButtonClickWriteMode)
            {
                return;
            }

            await WriteValueAsync(config, config.ButtonWriteValueText);
        }

        [RelayCommand]
        private async Task SubmitButtonPressWriteAsync(WriteDataPointConfig? config)
        {
            config ??= SelectedWriteDataPoint;
            if (config == null || !config.IsButtonPressAndReleaseMode)
            {
                return;
            }

            await WriteValueAsync(config, config.ButtonWriteValueText);
        }

        [RelayCommand]
        private async Task SubmitButtonReleaseWriteAsync(WriteDataPointConfig? config)
        {
            config ??= SelectedWriteDataPoint;
            if (config == null || !config.IsButtonPressAndReleaseMode)
            {
                return;
            }

            await WriteValueAsync(config, config.ButtonReleaseWriteValueText);
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
            
                Growl.Warning("当前写入点位未配置完整的设备和点位");
                return;
            }

            if (!config.IsEnabled)
            {
            
                Growl.Warning("当前写入点位未启用，禁止写入");
                return;
            }

            if (config.UsesWriteRestriction && !config.IsWriteInteractionEnabled)
            {
              
                Growl.Warning(string.IsNullOrWhiteSpace(config.WriteRestrictionBlockedReason) ? "限制条件未满足，禁止写入" : config.WriteRestrictionBlockedReason);
                return;
            }

            if (!config.RuntimeDevice.IsEnabled || !config.RuntimeDataPoint.IsEnabled)
            {
                Growl.Warning("设备或点位未启用，禁止写入");
                return;
            }

            if (string.IsNullOrWhiteSpace(rawValue) && config.DataType != FieldType.String)
            {
              
                Growl.Warning("请输入有效的写入值");
                return;
            }

            if (!WriteDataPointConfig.TryConvertWriteValue(rawValue, config.DataType, out var convertedValue, out var errorMessage))
            {
          
                Growl.Warning(errorMessage);
                return;
            }

            var (success, message) = await _plcDeviceRuntimeService.WriteDataPointValueAsync(config.RuntimeDevice, config.RuntimeDataPoint, convertedValue!);
            if (!success)
            {
               
                Growl.Warning(message ?? "写入失败");
                return;
            }

            config.RuntimeDataPoint.CurrentValue = convertedValue;
            config.CompleteValueEdit(convertedValue?.ToString() ?? string.Empty);
            
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
                ButtonInteractionMode = source.ButtonInteractionMode,
                ButtonWriteValueText = source.ButtonWriteValueText,
                ButtonReleaseWriteValueText = source.ButtonReleaseWriteValueText,
                ButtonDisplayText = source.ButtonDisplayText,
                RuleScriptText = source.RuleScriptText,
                EnableWriteRestriction = source.EnableWriteRestriction,
                WriteRestrictionScriptText = source.WriteRestrictionScriptText
            };

            foreach (var rule in source.DisplayRules)
            {
                clone.DisplayRules.Add(new WriteValueDisplayRule
                {
                    SourceValue = rule.SourceValue,
                    DisplayText = rule.DisplayText
                });
            }

            foreach (var variable in source.RestrictionVariables)
            {
                clone.RestrictionVariables.Add(new WriteValueRestrictionVariable
                {
                    VariableName = variable.VariableName,
                    PlcDeviceId = variable.PlcDeviceId,
                    DataPointId = variable.DataPointId
                });
            }

            AttachRestrictionVariables(clone);
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
            target.ButtonInteractionMode = source.ButtonInteractionMode;
            target.ButtonWriteValueText = source.ButtonWriteValueText;
            target.ButtonReleaseWriteValueText = source.ButtonReleaseWriteValueText;
            target.ButtonDisplayText = source.ButtonDisplayText;
            target.RuleScriptText = source.RuleScriptText;
            target.EnableWriteRestriction = source.EnableWriteRestriction;
            target.WriteRestrictionScriptText = source.WriteRestrictionScriptText;
            //target.SelectedDeviceName=source.SelectedDeviceName;
            target.DisplayRules.Clear();
            foreach (var rule in source.DisplayRules)
            {
                target.DisplayRules.Add(new WriteValueDisplayRule
                {
                    SourceValue = rule.SourceValue,
                    DisplayText = rule.DisplayText
                });
            }

            target.RestrictionVariables.Clear();
            foreach (var variable in source.RestrictionVariables)
            {
                target.RestrictionVariables.Add(new WriteValueRestrictionVariable
                {
                    VariableName = variable.VariableName,
                    PlcDeviceId = variable.PlcDeviceId,
                    DataPointId = variable.DataPointId
                });
            }

            AttachRestrictionVariables(target);
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

        private bool ValidateWriteRestriction(WriteDataPointConfig config, out string errorMessage)
        {
            if (!config.UsesWriteRestriction)
            {
                config.SetWriteRestrictionValidationState(true, string.Empty);
                config.SetWriteRestrictionEvaluationState(true, string.Empty);
                errorMessage = string.Empty;
                return true;
            }

            if (config.RestrictionVariables.Count == 0)
            {
                errorMessage = "限制模式至少需要配置一个变量";
                config.SetWriteRestrictionValidationState(false, errorMessage);
                config.SetWriteRestrictionEvaluationState(false, errorMessage);
                return false;
            }

            if (!TryBuildRestrictionVariables(config, allowMissingCurrentValue: true, out var variables, out errorMessage))
            {
                config.SetWriteRestrictionValidationState(false, errorMessage);
                config.SetWriteRestrictionEvaluationState(false, errorMessage);
                return false;
            }

            if (!_writeRestrictionScriptEvaluator.TryEvaluateScript(config.WriteRestrictionScriptText, variables, out _, out errorMessage))
            {
                config.SetWriteRestrictionValidationState(false, errorMessage);
                config.SetWriteRestrictionEvaluationState(false, errorMessage);
                return false;
            }

            config.SetWriteRestrictionValidationState(true, string.Empty);
            RefreshWriteRestrictionState(config);
            errorMessage = string.Empty;
            return true;
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

        private void RefreshWriteRestrictionState(WriteDataPointConfig config)
        {
            if (!config.UsesWriteRestriction)
            {
                config.SetWriteRestrictionValidationState(true, string.Empty);
                config.SetWriteRestrictionEvaluationState(true, string.Empty);
                return;
            }

            if (!TryBuildRestrictionVariables(config, allowMissingCurrentValue: false, out var variables, out var variablesErrorMessage))
            {
                config.SetWriteRestrictionEvaluationState(false, variablesErrorMessage);
                return;
            }

            if (!_writeRestrictionScriptEvaluator.TryEvaluateScript(config.WriteRestrictionScriptText, variables, out var isSatisfied, out var expressionErrorMessage))
            {
                config.SetWriteRestrictionValidationState(false, expressionErrorMessage);
                config.SetWriteRestrictionEvaluationState(false, expressionErrorMessage);
                return;
            }

            config.SetWriteRestrictionValidationState(true, string.Empty);
            config.SetWriteRestrictionEvaluationState(isSatisfied, isSatisfied ? string.Empty : "限制条件未满足，当前禁止写入和点击操作");
        }

        private bool TryBuildRestrictionVariables(WriteDataPointConfig config, bool allowMissingCurrentValue, out Dictionary<string, object?> variables, out string errorMessage)
        {
            variables = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var variable in config.RestrictionVariables)
            {
                if (string.IsNullOrWhiteSpace(variable.VariableName))
                {
                    errorMessage = "限制变量名不能为空";
                    return false;
                }

                if (!variables.TryAdd(variable.VariableName.Trim(), 0d))
                {
                    errorMessage = $"限制变量名重复：{variable.VariableName}";
                    return false;
                }

                if (variable.RuntimeDevice == null)
                {
                    errorMessage = $"变量 {variable.VariableName} 未选择设备";
                    return false;
                }

                if (variable.RuntimeDataPoint == null)
                {
                    errorMessage = $"变量 {variable.VariableName} 未选择点位";
                    return false;
                }

                if (!TryGetRestrictionVariableValue(variable.RuntimeDataPoint.CurrentValue, allowMissingCurrentValue, out var currentValue, out errorMessage))
                {
                    errorMessage = $"变量 {variable.VariableName} 取值失败：{errorMessage}";
                    return false;
                }

                variables[variable.VariableName.Trim()] = currentValue;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static bool TryGetRestrictionVariableValue(object? value, bool allowMissingCurrentValue, out object? normalizedValue, out string errorMessage)
        {
            normalizedValue = null;
            if (value == null)
            {
                if (allowMissingCurrentValue)
                {
                    normalizedValue = 0d;
                    errorMessage = string.Empty;
                    return true;
                }

                errorMessage = "当前没有采集值";
                return false;
            }

            switch (value)
            {
                case bool boolValue:
                    normalizedValue = boolValue;
                    errorMessage = string.Empty;
                    return true;
                case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                    normalizedValue = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    errorMessage = string.Empty;
                    return true;
                case string stringValue:
                    normalizedValue = stringValue;
                    errorMessage = string.Empty;
                    return true;
            }

            normalizedValue = value.ToString();
            errorMessage = string.Empty;
            return true;
        }

        private void RefreshCurrentValueDisplayText(WriteDataPointConfig config)
        {
            config.CurrentValueDisplayText = _writeValueLabelRuleService.GetDisplayText(
                config.RuntimeDataPoint?.CurrentValue,
                config.UsesRuleDisplay,
                config.DisplayRules,
                config.DefaultDisplayText);
            RefreshWriteRestrictionState(config);
        }

        private int GetNextWriteDataPointIndex()
        {
            return WriteDataPoints.Count == 0 ? 1 : WriteDataPoints.Max(x => x.Index) + 1;
        }



        public void MoveWriteDataPoint(WriteDataPointConfig source, WriteDataPointConfig? target)
        {
            if (source is null)
            {
                return;
            }

            if (WriteDataPoints.Count == 0)
            {
                return;
            }

            int oldIndex = WriteDataPoints.IndexOf(source);
            if (oldIndex < 0)
            {
                return;
            }

            int newIndex = target is null
                ? WriteDataPoints.Count - 1
                : WriteDataPoints.IndexOf(target);

            if (newIndex < 0 || newIndex == oldIndex)
            {
                return;
            }

            WriteDataPoints.Move(oldIndex, newIndex);
            WriteDataPoints.Select((x, i) => { x.Index = i + 1; return x; }).ToList();

        }




        [RelayCommand]
        private void UpChannel()
        {
            if (SelectedWriteDataPoint == null)
            {
                Growl.Warning("请先选中要上移的通道");
                return;
            }

            if (WriteDataPoints.Count <= 1)
            {
                return;
            }

            int currentIndex = WriteDataPoints.IndexOf(SelectedWriteDataPoint);
            if (currentIndex < 0)
            {
                Growl.Warning("当前选中通道无效");
                return;
            }

            if (currentIndex == 0)
            {
                Growl.Info("当前已是第一行");
                return;
            }

            WriteDataPoints.Move(currentIndex, currentIndex - 1);
            UpdateWriteDataPointIndexes(currentIndex - 1, currentIndex);
        }
        [RelayCommand]
        private void DownChannel()
        {
            if (SelectedWriteDataPoint == null)
            {
                Growl.Warning("请先选中要下移的通道");
                return;
            }

            if (WriteDataPoints.Count <= 1)
            {
                return;
            }

            int currentIndex = WriteDataPoints.IndexOf(SelectedWriteDataPoint);
            if (currentIndex < 0)
            {
                Growl.Warning("当前选中通道无效");
                return;
            }

            if (currentIndex >= WriteDataPoints.Count - 1)
            {
                Growl.Info("当前已是最后一行");
                return;
            }

            WriteDataPoints.Move(currentIndex, currentIndex + 1);
            UpdateWriteDataPointIndexes(currentIndex, currentIndex + 1);
        }
        private void UpdateWriteDataPointIndexes(int startIndex, int endIndex)
        {
            if (WriteDataPoints.Count == 0)
            {
                return;
            }

            if (startIndex < 0)
            {
                startIndex = 0;
            }

            if (endIndex >= WriteDataPoints.Count)
            {
                endIndex = WriteDataPoints.Count - 1;
            }

            for (int i = startIndex; i <= endIndex; i++)
            {
                int newDisplayIndex = i + 1;
                if (WriteDataPoints[i].Index != newDisplayIndex)
                {
                    WriteDataPoints[i].Index = newDisplayIndex;
                }
            }
        }
    }
}