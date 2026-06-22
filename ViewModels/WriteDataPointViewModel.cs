using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using MeasurementSoftware.Extensions;
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
using System.Windows.Threading;
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
        private ObservableCollection<WriteDataPointPageConfig>? _observedWriteDataPointPages;

        private PropertyChangedEventHandler? _editingRuntimeDataPointPropertyChangedHandler;
        private WriteDataPointConfig? selectedWriteDataPoint;
        private WriteDataPointPageConfig? selectedEditPage;
        private WriteDataPointPageConfig? selectedDisplayPage;
        private WriteDataPointConfig? editingWriteDataPoint;
        private WriteValueDisplayRule? selectedDisplayRule;
        private WriteValueRestrictionVariable? selectedRestrictionVariable;
        private bool isEditorOpen;
        private bool isEditMode;
        public MeasurementRecipe? CurrentRecipe => _recipeConfigService.CurrentRecipe;

        public bool HasRecipe => CurrentRecipe != null;

        public ObservableCollection<WriteDataPointConfig> WriteDataPoints => CurrentRecipe?.OtherSettings.WriteDataPoints ?? [];

        public ObservableCollection<WriteDataPointPageConfig> WriteDataPointPages => CurrentRecipe?.OtherSettings.WriteDataPointPages ?? [];

        public ObservableCollection<WriteDataPointConfig> EnabledWriteDataPoints { get; set; } = [];

        public ObservableCollection<WriteDataPointConfig> CurrentPageWriteDataPoints { get; } = [];

        public ObservableCollection<WriteDataPointConfig> CurrentPageEnabledWriteDataPoints { get; } = [];

        public ReadOnlyObservableCollection<PlcDevice> EnabledPlcDevices => _enabledDevicesObserver.EnabledDevicesView;

        public IEnumerable<WriteValueEditorMode> EditorModes => Enum.GetValues<WriteValueEditorMode>();

        public IEnumerable<WriteValueLabelDisplayMode> LabelDisplayModes => Enum.GetValues<WriteValueLabelDisplayMode>();

        public IEnumerable<WriteValueButtonInteractionMode> ButtonInteractionModes => Enum.GetValues<WriteValueButtonInteractionMode>();

        public IEnumerable<FieldType> DataTypes => Enum.GetValues<FieldType>();

        /// <summary>
        /// 批量选中页面进行移动
        /// </summary>
        public ObservableCollection<WriteDataPointConfig> SelectedWriteDataPoints { get; } = [];

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

        public WriteDataPointPageConfig? SelectedEditPage
        {
            get => selectedEditPage;
            set
            {
                SetProperty(ref selectedEditPage, value, (() =>
                {
                    RefreshCurrentPageWriteDataPoints();
                }));
            }
        }

        public WriteDataPointPageConfig? SelectedDisplayPage
        {
            get => selectedDisplayPage;
            set
            {
                if (!SetProperty(ref selectedDisplayPage, value))
                {
                    return;
                }

                RefreshCurrentPageEnabledWriteDataPoints();
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
            OnPropertyChanged(nameof(WriteDataPointPages));
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

            if (_observedWriteDataPointPages != null)
            {
                _observedWriteDataPointPages.CollectionChanged -= WriteDataPointPages_CollectionChanged;
                foreach (var page in _observedWriteDataPointPages)
                {
                    page.PropertyChanged -= WriteDataPointPageConfig_PropertyChanged;
                }
            }

            _observedWriteDataPoints = CurrentRecipe?.OtherSettings.WriteDataPoints;
            _observedWriteDataPointPages = CurrentRecipe?.OtherSettings.WriteDataPointPages;
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

            if (_observedWriteDataPointPages != null)
            {
                _observedWriteDataPointPages.CollectionChanged -= WriteDataPointPages_CollectionChanged;
                _observedWriteDataPointPages.CollectionChanged += WriteDataPointPages_CollectionChanged;
                foreach (var page in _observedWriteDataPointPages)
                {
                    page.PropertyChanged -= WriteDataPointPageConfig_PropertyChanged;
                    page.PropertyChanged += WriteDataPointPageConfig_PropertyChanged;
                }
            }

            EnsureSelectedPages();
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

            EnsureSelectedPages();
            NormalizeWriteDataPointPageOrder();
            RefreshEnabledWriteDataPoints();
            OnPropertyChanged(nameof(WriteDataPoints));
        }

        private void WriteDataPointPages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (WriteDataPointPageConfig page in e.OldItems)
                {
                    page.PropertyChanged -= WriteDataPointPageConfig_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (WriteDataPointPageConfig page in e.NewItems)
                {
                    page.PropertyChanged -= WriteDataPointPageConfig_PropertyChanged;
                    page.PropertyChanged += WriteDataPointPageConfig_PropertyChanged;
                }
            }

            EnsureSelectedPages();
            RefreshCurrentPageWriteDataPoints();
            RefreshCurrentPageEnabledWriteDataPoints();
            OnPropertyChanged(nameof(WriteDataPointPages));
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
                or nameof(WriteDataPointConfig.RuntimeDataPoint)
                or nameof(WriteDataPointConfig.PageIndex)
                or nameof(WriteDataPointConfig.PageOrder))
            {
                RefreshCurrentValueDisplayText(config);
            }

            if (e.PropertyName is nameof(WriteDataPointConfig.PageIndex)
                or nameof(WriteDataPointConfig.PageOrder)
                or nameof(WriteDataPointConfig.IsEnabled)
                or nameof(WriteDataPointConfig.DisplayName))
            {
                RefreshCurrentPageWriteDataPoints();
                RefreshCurrentPageEnabledWriteDataPoints();
            }
        }

        private void WriteDataPointPageConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(WriteDataPointPageConfig.PageName)
                or nameof(WriteDataPointPageConfig.PageIndex)
                or nameof(WriteDataPointPageConfig.Order))
            {
                EnsureSelectedPages();
                RefreshCurrentPageWriteDataPoints();
                RefreshCurrentPageEnabledWriteDataPoints();
                OnPropertyChanged(nameof(WriteDataPointPages));
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

            EnsureSelectedPages();
            NormalizeWriteDataPointPageOrder();
            RefreshEnabledWriteDataPoints();
        }


        private void RefreshEnabledWriteDataPoints()
        {
            var enabledItems = WriteDataPoints
                .Where(x => x.IsEnabled)
                .OrderBy(x => x.PageIndex)
                .ThenBy(x => x.PageOrder)
                .ThenBy(x => x.Index)
                .ToList();

            EnabledWriteDataPoints.Clear();
            foreach (var enabledItem in enabledItems)
            {
                EnabledWriteDataPoints.Add(enabledItem);
            }

            if (SelectedWriteDataPoint != null && !SelectedWriteDataPoint.IsEnabled && EnabledWriteDataPoints.Count > 0)
            {
                SelectedWriteDataPoint = EnabledWriteDataPoints.FirstOrDefault();
            }

            RefreshCurrentPageWriteDataPoints();
            RefreshCurrentPageEnabledWriteDataPoints();
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
                PageIndex = GetCurrentEditPageIndex(),
                PageOrder = GetNextPageOrder(GetCurrentEditPageIndex()),
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

            if (EditingWriteDataPoint.IsEnableTextBoxMode)
            {

                var minError = EditingWriteDataPoint[nameof(EditingWriteDataPoint.ValidationMinValue)];
                if (!string.IsNullOrWhiteSpace(minError))
                {
                    Growl.Warning(minError);
                    return;
                }

                var maxError = EditingWriteDataPoint[nameof(EditingWriteDataPoint.ValidationMaxValue)];
                if (!string.IsNullOrWhiteSpace(maxError))
                {
                    Growl.Warning(maxError);
                    return;
                }

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

            int deletedPageIndex = SelectedWriteDataPoint.PageIndex;
            WriteDataPoints.Remove(SelectedWriteDataPoint);
            NormalizeWriteDataPointPageOrder(deletedPageIndex);
            SelectedWriteDataPoint = GetWriteDataPointsByPage(deletedPageIndex).FirstOrDefault() ?? WriteDataPoints.FirstOrDefault();
            Growl.Success("写入点位已删除");
        }

        [RelayCommand]
        private void ResetWriteDataPointOrder()
        {
            if (WriteDataPoints.Count <= 1)
            {
                return;
            }

            NormalizeWriteDataPointPageOrder();

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
            if (config.IsEnableTextBoxMode)
            {
                if (double.TryParse(rawValue, out var value))
                {
                    if (!string.IsNullOrWhiteSpace(config.ValidationMinValue) &&
                        double.TryParse(config.ValidationMinValue, out var minValue))
                    {
                        if (value < minValue)
                        {
                            Growl.Warning($"写入值不能小于下限值 {minValue}");
                            return;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(config.ValidationMaxValue) &&
                        double.TryParse(config.ValidationMaxValue, out var maxValue))
                    {
                        if (value > maxValue)
                        {
                            Growl.Warning($"写入值不能大于上限值 {maxValue}");
                            return;
                        }
                    }
                }
                //else
                //{
                //    Growl.Warning("请输入有效数字");
                //    return;
                //}
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
                PageIndex = source.PageIndex,
                PageOrder = source.PageOrder,
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
                WriteRestrictionScriptText = source.WriteRestrictionScriptText,
                EnableTextBoxValidation = source.EnableTextBoxValidation,
                ValidationMinValue = source.ValidationMinValue,
                ValidationMaxValue = source.ValidationMaxValue
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
            target.PageIndex = source.PageIndex;
            target.PageOrder = source.PageOrder;
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
            target.EnableTextBoxValidation = source.EnableTextBoxValidation;
            target.ValidationMaxValue = source.ValidationMaxValue;
            target.ValidationMinValue = source.ValidationMinValue;
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

        /// <summary>
        /// 获取当前编辑页签的页码。
        /// </summary>
        private int GetCurrentEditPageIndex()
        {
            return SelectedEditPage?.PageIndex ?? WriteDataPointPages.FirstOrDefault()?.PageIndex ?? 1;
        }

        /// <summary>
        /// 获取当前页签可删除时可移动到的目标页签。
        /// 第一页不允许删除，因此第一页不提供删除迁移目标。
        /// </summary>
        public IEnumerable<WriteDataPointPageConfig> OtherEditPages => SelectedEditPage == null || SelectedEditPage.PageIndex <= 1
            ? []
            : WriteDataPointPages.Where(x => !ReferenceEquals(x, SelectedEditPage));

        /// <summary>
        /// 获取当前选中通道可移动到的目标页签。
        /// </summary>
        public IEnumerable<WriteDataPointPageConfig> OtherPagesForSelectedChannel => SelectedEditPage == null
            ? []
            : WriteDataPointPages.Where(x => x.PageIndex != SelectedEditPage.PageIndex);

        /// <summary>
        /// 获取当前页签中启用通道批量移动可用的目标页签。
        /// </summary>
        public IEnumerable<WriteDataPointPageConfig> OtherPagesForEnabledChannels => SelectedEditPage == null
            ? []
            : WriteDataPointPages.Where(x => x.PageIndex != SelectedEditPage.PageIndex);

        /// <summary>
        /// 获取当前页签中未启用通道批量移动可用的目标页签。
        /// </summary>
        public IEnumerable<WriteDataPointPageConfig> OtherPagesForDisabledChannels => SelectedEditPage == null
            ? []
            : WriteDataPointPages.Where(x => x.PageIndex != SelectedEditPage.PageIndex);


        public List<WriteDataPointPageConfig> TargetPagesForBatchEnableMove => SelectedEditPage == null
        ? []
        : WriteDataPointPages
            .Where(x => x.PageIndex != SelectedEditPage.PageIndex)
            .ToList();

        /// <summary>
        /// 获取目标页签中下一个页内顺序号。
        /// </summary>
        private int GetNextPageOrder(int pageIndex)
        {
            var items = WriteDataPoints.Where(x => x.PageIndex == pageIndex).ToList();
            return items.Count == 0 ? 1 : items.Max(x => x.PageOrder) + 1;
        }

        /// <summary>
        /// 获取指定页签下的全部写入点位。
        /// </summary>
        private List<WriteDataPointConfig> GetWriteDataPointsByPage(int pageIndex)
        {
            return WriteDataPoints
                .Where(x => x.PageIndex == pageIndex)
                .OrderBy(x => x.PageOrder)
                .ThenBy(x => x.Index)
                .ToList();
        }

        /// <summary>
        /// 获取指定页签下已启用的写入点位。
        /// </summary>
        private List<WriteDataPointConfig> GetEnabledWriteDataPointsByPage(int pageIndex)
        {
            return EnabledWriteDataPoints
                .Where(x => x.PageIndex == pageIndex)
                .OrderBy(x => x.PageOrder)
                .ThenBy(x => x.Index)
                .ToList();
        }

        /// <summary>
        /// 确保编辑页和显示页始终有有效的选中页签。
        /// </summary>
        private void EnsureSelectedPages()
        {
            if (SelectedEditPage == null || !WriteDataPointPages.Contains(SelectedEditPage))
            {
                SelectedEditPage = WriteDataPointPages.FirstOrDefault();
            }

            if (SelectedDisplayPage == null || !WriteDataPointPages.Contains(SelectedDisplayPage))
            {
                SelectedDisplayPage = WriteDataPointPages.FirstOrDefault();
            }
        }

        /// <summary>
        /// 刷新当前编辑页签下的写入点位集合。
        /// </summary>
        private void RefreshCurrentPageWriteDataPoints()
        {
            var pageIndex = GetCurrentEditPageIndex();
            var items = GetWriteDataPointsByPage(pageIndex);

            CurrentPageWriteDataPoints.Clear();
            foreach (var item in items)
            {
                CurrentPageWriteDataPoints.Add(item);
            }

            if (SelectedWriteDataPoint != null && SelectedWriteDataPoint.PageIndex != pageIndex)
            {
                SelectedWriteDataPoint = items.FirstOrDefault() ?? SelectedWriteDataPoint;
            }

            OnPropertyChanged(nameof(CurrentPageWriteDataPoints));
            OnPropertyChanged(nameof(OtherEditPages));
            OnPropertyChanged(nameof(OtherPagesForSelectedChannel));
            OnPropertyChanged(nameof(OtherPagesForEnabledChannels));
            OnPropertyChanged(nameof(OtherPagesForDisabledChannels));
            OnPropertyChanged(nameof(TargetPagesForBatchEnableMove));
        }

        /// <summary>
        /// 刷新当前显示页签下的启用写入点位集合。
        /// </summary>
        private void RefreshCurrentPageEnabledWriteDataPoints()
        {
            var pageIndex = SelectedDisplayPage?.PageIndex ?? WriteDataPointPages.FirstOrDefault()?.PageIndex ?? 1;
            var items = GetEnabledWriteDataPointsByPage(pageIndex);

            CurrentPageEnabledWriteDataPoints.Clear();
            foreach (var item in items)
            {
                CurrentPageEnabledWriteDataPoints.Add(item);
            }

            OnPropertyChanged(nameof(CurrentPageEnabledWriteDataPoints));
        }

        /// <summary>
        /// 重排所有页签中的通道顺序和全局编号。
        /// </summary>
        private void NormalizeWriteDataPointPageOrder()
        {
            foreach (var page in WriteDataPointPages.OrderBy(x => x.PageIndex))
            {
                NormalizeWriteDataPointPageOrder(page.PageIndex);
            }

            WriteDataPoints
                .OrderBy(x => x.PageIndex)
                .ThenBy(x => x.PageOrder)
                .ThenBy(x => x.Index)
                .Select((x, i) =>
                {
                    x.Index = i + 1;
                    return x;
                })
                .ToList();

            RefreshCurrentPageWriteDataPoints();
            RefreshCurrentPageEnabledWriteDataPoints();
        }

        /// <summary>
        /// 重排指定页签中的通道顺序。
        /// </summary>
        private void NormalizeWriteDataPointPageOrder(int pageIndex)
        {
            var items = GetWriteDataPointsByPage(pageIndex);
            for (int index = 0; index < items.Count; index++)
            {
                items[index].PageOrder = index + 1;
            }
        }

        /// <summary>
        /// 设置页签编辑状态，仅允许一个页签处于编辑中。
        /// </summary>
        private void SetPageEditingState(WriteDataPointPageConfig? page, bool isEditing)
        {
            if (!isEditing)
            {
                foreach (var item in WriteDataPointPages)
                {
                    item.IsEditing = false;
                }

                return;
            }

            if (page == null)
            {
                return;
            }

            foreach (var item in WriteDataPointPages)
            {
                if (ReferenceEquals(item, page))
                {
                    item.EditingPageName = item.PageName;
                }

                item.IsEditing = ReferenceEquals(item, page) && isEditing;
            }
        }

        /// <summary>
        /// 将指定通道移动到目标页签，并插入到第一页首位。
        /// </summary>
        private void MoveChannelToPage(WriteDataPointConfig config, int targetPageIndex)
        {
            if (config.PageIndex == targetPageIndex)
            {
                return;
            }

            int sourcePageIndex = config.PageIndex;
            foreach (var item in GetWriteDataPointsByPage(targetPageIndex))
            {
                item.PageOrder += 1;
            }

            config.PageIndex = targetPageIndex;
            config.PageOrder = 1;
            NormalizeWriteDataPointPageOrder(sourcePageIndex);
            NormalizeWriteDataPointPageOrder(targetPageIndex);
            SelectedEditPage = WriteDataPointPages.FirstOrDefault(x => x.PageIndex == targetPageIndex);
            RefreshEnabledWriteDataPoints();
        }






        private void MoveChannelsToPage(WriteDataPointConfig item, int targetPageIndex)
        {
            var targetItems = GetWriteDataPointsByPage(targetPageIndex).ToList();
            int nextOrder = targetItems.Count + 1;

            item.PageIndex = targetPageIndex;
            item.PageOrder = nextOrder;

            NormalizeWriteDataPointPageOrder(item.PageIndex);
            NormalizeWriteDataPointPageOrder(targetPageIndex);
        }


        /// <summary>
        /// 将当前页中满足条件的通道批量移动到目标页，并插入目标页最后面。
        /// </summary>
        private void MoveChannelsToPage(int sourcePageIndex, int targetPageIndex, Func<WriteDataPointConfig, bool> predicate)
        {
            var sourceItems = GetWriteDataPointsByPage(sourcePageIndex)
                .Where(predicate)
                .ToList();

            if (sourceItems.Count == 0)
            {
                return;
            }

            var targetItems = GetWriteDataPointsByPage(targetPageIndex).ToList();
            int startOrder = targetItems.Count + 1;

            for (int index = 0; index < sourceItems.Count; index++)
            {
                sourceItems[index].PageIndex = targetPageIndex;
                sourceItems[index].PageOrder = startOrder + index;
            }

            NormalizeWriteDataPointPageOrder(sourcePageIndex);
            NormalizeWriteDataPointPageOrder(targetPageIndex);

            SelectedEditPage = WriteDataPointPages.FirstOrDefault(x => x.PageIndex == targetPageIndex);
            RefreshEnabledWriteDataPoints();
        }

        /// <summary>
        /// 删除页签后，统一重排页码和页签顺序。
        /// </summary>
        private void ReindexWriteDataPointPages()
        {
            var orderedPages = WriteDataPointPages.OrderBy(x => x.Order).ThenBy(x => x.PageIndex).ToList();
            var pageIndexMap = orderedPages.ToDictionary(page => page.PageIndex, _ => 0);

            for (int index = 0; index < orderedPages.Count; index++)
            {
                var page = orderedPages[index];
                int newPageIndex = index + 1;
                pageIndexMap[page.PageIndex] = newPageIndex;
            }

            foreach (var item in WriteDataPoints)
            {
                if (pageIndexMap.TryGetValue(item.PageIndex, out int newPageIndex))
                {
                    item.PageIndex = newPageIndex;
                }
            }

            for (int index = 0; index < orderedPages.Count; index++)
            {
                orderedPages[index].PageIndex = index + 1;
                orderedPages[index].Order = index;
                if (string.IsNullOrWhiteSpace(orderedPages[index].PageName))
                {
                    orderedPages[index].PageName = $"页面{index + 1}";
                }
            }
        }



        /// <summary>
        /// 在当前页签内拖拽移动通道顺序。
        /// </summary>
        public void MoveWriteDataPoint(WriteDataPointConfig source, WriteDataPointConfig? target)
        {
            if (source is null)
            {
                return;
            }

            if (CurrentPageWriteDataPoints.Count == 0)
            {
                return;
            }

            int oldIndex = CurrentPageWriteDataPoints.IndexOf(source);
            if (oldIndex < 0)
            {
                return;
            }

            int newIndex = target is null
                ? CurrentPageWriteDataPoints.Count - 1
                : CurrentPageWriteDataPoints.IndexOf(target);

            if (newIndex < 0 || newIndex == oldIndex)
            {
                return;
            }

            var pageItems = GetWriteDataPointsByPage(source.PageIndex);
            pageItems.Remove(source);
            pageItems.Insert(newIndex, source);
            for (int index = 0; index < pageItems.Count; index++)
            {
                pageItems[index].PageOrder = index + 1;
            }

            NormalizeWriteDataPointPageOrder();

        }




        [RelayCommand]
        /// <summary>
        /// 当前页签内上移选中通道。
        /// </summary>
        private void UpChannel()
        {
            var selectedItem = SelectedWriteDataPoint;
            if (selectedItem == null)
            {
                Growl.Warning("请先选中要上移的通道");
                return;
            }

            var pageItems = GetWriteDataPointsByPage(selectedItem.PageIndex);
            if (pageItems.Count <= 1)
            {
                return;
            }

            int currentIndex = pageItems.IndexOf(selectedItem);
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

            var target = pageItems[currentIndex - 1];
            int targetOrder = target.PageOrder;
            target.PageOrder = selectedItem.PageOrder;
            selectedItem.PageOrder = targetOrder;
            NormalizeWriteDataPointPageOrder();
            SelectedWriteDataPoint = selectedItem;
        }

        [RelayCommand]
        /// <summary>
        /// 当前页签内下移选中通道。
        /// </summary>
        private void DownChannel()
        {
            var selectedItem = SelectedWriteDataPoint;
            if (selectedItem == null)
            {
                Growl.Warning("请先选中要下移的通道");
                return;
            }

            var pageItems = GetWriteDataPointsByPage(selectedItem.PageIndex);
            if (pageItems.Count <= 1)
            {
                return;
            }

            int currentIndex = pageItems.IndexOf(selectedItem);
            if (currentIndex < 0)
            {
                Growl.Warning("当前选中通道无效");
                return;
            }

            if (currentIndex >= pageItems.Count - 1)
            {
                Growl.Info("当前已是最后一行");
                return;
            }

            var target = pageItems[currentIndex + 1];
            int targetOrder = target.PageOrder;
            target.PageOrder = selectedItem.PageOrder;
            selectedItem.PageOrder = targetOrder;
            NormalizeWriteDataPointPageOrder();
            SelectedWriteDataPoint = selectedItem;
        }

        [RelayCommand]
        /// <summary>
        /// 将选中通道左移到前一个页签。
        /// </summary>
        private void MoveToLeftPage()
        {
            if (SelectedWriteDataPoint == null)
            {
                Growl.Warning("请先选中要移动的通道");
                return;
            }

            if (SelectedWriteDataPoint.PageIndex <= 1)
            {
                Growl.Info("当前已是第一页");
                return;
            }

            SelectedWriteDataPoint.PageIndex -= 1;
            SelectedWriteDataPoint.PageOrder = GetNextPageOrder(SelectedWriteDataPoint.PageIndex);
            NormalizeWriteDataPointPageOrder(SelectedWriteDataPoint.PageIndex + 1);
            NormalizeWriteDataPointPageOrder(SelectedWriteDataPoint.PageIndex);
            SelectedEditPage = WriteDataPointPages.FirstOrDefault(x => x.PageIndex == SelectedWriteDataPoint.PageIndex);
            RefreshEnabledWriteDataPoints();
        }

        [RelayCommand]
        /// <summary>
        /// 将选中通道移动到指定页签。
        /// </summary>
        private void MoveChannelToPage(WriteDataPointPageConfig? page)
        {
            var movedItem = SelectedWriteDataPoint;
            if (movedItem == null)
            {
                Growl.Warning("请先选中要移动的通道");
                return;
            }

            if (page == null)
            {
                Growl.Warning("请选择目标页面");
                return;
            }

            MoveChannelsToPage(movedItem, page.PageIndex);


            // 切到目标页
            SelectedEditPage = page;

            // 重新选中移动后的那一条
            SelectedWriteDataPoint = movedItem;

            // 刷新显示页点位
            //RefreshEnabledWriteDataPoints();
        }



        [RelayCommand]
        private void MoveSelectedChannels(WriteDataPointPageConfig? page)
        {
            if (page == null)
            {
                return;
            }

            var selectedSet = SelectedWriteDataPoints.ToHashSet();

            MoveChannelsToPage(
                SelectedEditPage!.PageIndex,
                page.PageIndex,
                item => selectedSet.Contains(item));
            SelectedWriteDataPoint = selectedSet.FirstOrDefault();
        }

        [RelayCommand]
        /// <summary>
        /// 将选中通道右移到后一个页签。
        /// </summary>
        private void MoveToRightPage()
        {
            if (SelectedWriteDataPoint == null)
            {
                Growl.Warning("请先选中要移动的通道");
                return;
            }

            int maxPageIndex = WriteDataPointPages.Count == 0 ? 1 : WriteDataPointPages.Max(x => x.PageIndex);
            if (SelectedWriteDataPoint.PageIndex >= maxPageIndex)
            {
                Growl.Info("当前已是最后一页");
                return;
            }

            SelectedWriteDataPoint.PageIndex += 1;
            SelectedWriteDataPoint.PageOrder = GetNextPageOrder(SelectedWriteDataPoint.PageIndex);
            NormalizeWriteDataPointPageOrder(SelectedWriteDataPoint.PageIndex - 1);
            NormalizeWriteDataPointPageOrder(SelectedWriteDataPoint.PageIndex);
            SelectedEditPage = WriteDataPointPages.FirstOrDefault(x => x.PageIndex == SelectedWriteDataPoint.PageIndex);
            RefreshEnabledWriteDataPoints();
        }

        [RelayCommand]
        /// <summary>
        /// 在最后新增一个页面，并立即进入名称编辑。
        /// </summary>
        private void AddWriteDataPointPage()
        {
            if (CurrentRecipe == null)
            {
                return;
            }

            int nextPageIndex = WriteDataPointPages.Count == 0 ? 1 : WriteDataPointPages.Max(x => x.PageIndex) + 1;
            var page = new WriteDataPointPageConfig
            {
                PageIndex = nextPageIndex,
                Order = WriteDataPointPages.Count,
                PageName = $"页面{nextPageIndex}"
            };

            WriteDataPointPages.Add(page);
            SelectedEditPage = page;
            SelectedDisplayPage = page;
            SetPageEditingState(page, true);
            OnPropertyChanged(nameof(OtherEditPages));
            OnPropertyChanged(nameof(OtherPagesForSelectedChannel));
        }
        [RelayCommand]
        private void BeginEditWriteDataPointPage(WriteDataPointPageConfig? page)
        {
            page ??= SelectedEditPage;
            if (page == null)
            {
                return;
            }

            SelectedEditPage = page;
            //SelectedDisplayPage = page;
            SetPageEditingState(page, true);
        }
        [RelayCommand]
        /// <summary>
        /// 提交页签名称编辑。
        /// </summary>
        private void ConfirmWriteDataPointPageEdit(WriteDataPointPageConfig? page)
        {
            page ??= SelectedEditPage;
            if (page == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(page.EditingPageName))
            {
                page.EditingPageName = $"页面{page.PageIndex}";
            }

            page.PageName = page.EditingPageName;
            SetPageEditingState(page, false);
        }

        /// <summary>
        /// 取消页签名称编辑并恢复原名称。
        /// </summary>
        public void CancelWriteDataPointPageEdit(WriteDataPointPageConfig? page)
        {
            page ??= SelectedEditPage;
            if (page == null)
            {
                return;
            }

            page.EditingPageName = page.PageName;
            SetPageEditingState(page, false);
        }

        /// <summary>
        /// 供行为调用的页签名称提交入口。
        /// </summary>
        public void CommitWriteDataPointPageEdit(WriteDataPointPageConfig? page)
        {
            ConfirmWriteDataPointPageEdit(page);
        }

        [RelayCommand]
        /// <summary>
        /// 删除页签；如果页内有通道则提示会清空全部通道，无通道则直接删除。
        /// </summary>
        private void DeleteWriteDataPointPageAndClearChannels(WriteDataPointPageConfig? page)
        {
            page ??= SelectedEditPage;
            if (page == null)
            {
                Growl.Warning("请选择要删除的页面");
                return;
            }

            if (WriteDataPointPages.Count <= 1 || page.PageIndex == 1)
            {
                Growl.Warning("第一个页面必须保留");
                return;
            }

            var pageItems = WriteDataPoints.Where(x => x.PageIndex == page.PageIndex).ToList();
            if (pageItems.Count > 0)
            {
                var result = MessageBox.Show($"删除页面“{page.PageName}”会清空里面的所有通道，确定继续吗？", "提示", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
                if (result != System.Windows.MessageBoxResult.Yes)
                {
                    return;
                }
            }

            foreach (var item in pageItems)
            {
                WriteDataPoints.Remove(item);
            }

            WriteDataPointPages.Remove(page);
            SetPageEditingState(null, false);
            ReindexWriteDataPointPages();
            EnsureSelectedPages();
            NormalizeWriteDataPointPageOrder();
        }

        [RelayCommand]
        /// <summary>
        /// 删除当前页签，并将通道整体移动到目标页签。
        /// </summary>
        private void DeleteWriteDataPointPageAndMoveChannels(WriteDataPointPageConfig? targetPage)
        {
            var sourcePage = SelectedEditPage;
            if (sourcePage == null || targetPage == null)
            {
                Growl.Warning("请选择有效的源页面和目标页面");
                return;
            }

            if (ReferenceEquals(sourcePage, targetPage))
            {
                Growl.Warning("目标页面不能与当前页面相同");
                return;
            }
            var pageItems = WriteDataPoints.Where(x => x.PageIndex == sourcePage.PageIndex).ToList();
            if (pageItems.Count > 0)
            {
                var result = MessageBox.Show($"确定删除页面“{sourcePage.PageName}”并将通道移动到“{targetPage.PageName}”吗？", "提示", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
                if (result != System.Windows.MessageBoxResult.Yes)
                {
                    return;
                }
            }
            var sourceItems = GetWriteDataPointsByPage(sourcePage.PageIndex);
            foreach (var targetItem in GetWriteDataPointsByPage(targetPage.PageIndex))
            {
                targetItem.PageOrder += sourceItems.Count;
            }

            for (int index = 0; index < sourceItems.Count; index++)
            {
                sourceItems[index].PageIndex = targetPage.PageIndex;
                sourceItems[index].PageOrder = index + 1;
            }

            WriteDataPointPages.Remove(sourcePage);
            SetPageEditingState(null, false);
            ReindexWriteDataPointPages();
            EnsureSelectedPages();
            NormalizeWriteDataPointPageOrder();
        }

        [RelayCommand]
        /// <summary>
        /// 删除页面入口，统一按“直接删除；若有通道则先提示清空”处理。
        /// </summary>
        private void DeleteWriteDataPointPage(WriteDataPointPageConfig? page)
        {
            DeleteWriteDataPointPageAndClearChannels(page);
        }
    }
}