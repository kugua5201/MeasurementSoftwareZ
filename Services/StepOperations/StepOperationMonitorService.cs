using MeasurementSoftware.Extensions;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Logs;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;

namespace MeasurementSoftware.Services.StepOperations
{
    /// <summary>
    /// 工步操作监听服务。
    /// 负责监听配方中的点位绑定，并在满足触发条件时抛出统一动作事件。
    /// </summary>
    public class StepOperationMonitorService : IStepOperationMonitorService
    {
        private readonly Lock _syncRoot = new();
        private readonly ILog _log;

        private MeasurementRecipe? _recipe;
        private RecipeOtherSettingsConfig? _observedSettings;
        private ObservableCollection<StepOperationBindingConfig>? _observedBindings;
        private ObservableCollection<PlcDevice>? _observedDevices;
        private CancellationTokenSource? _monitorCts;
        private bool _isRefreshingBindings;

        private int _delayTime => _recipe?.OtherSettings.AcquisitionDelayMs ?? 100;

        public StepOperationMonitorService(ILog log)
        {
            _log = log;
        }

        /// <inheritdoc/>
        public event EventHandler<StepOperationTriggeredEventArgs>? OperationTriggered;

        /// <inheritdoc/>
        public void SetRecipe(MeasurementRecipe? recipe)
        {
            if (ReferenceEquals(_recipe, recipe))
            {
                ApplyMonitorState();
                return;
            }

            UnsubscribeRecipe();
            StopMonitor();

            _recipe = recipe;

            SubscribeRecipe();
            ApplyMonitorState();
        }

        private void SubscribeRecipe()
        {
            if (_recipe is INotifyPropertyChanged recipeNotify)
            {
                recipeNotify.PropertyChanged += Recipe_PropertyChanged;
            }

            SubscribeOtherSettings(_recipe?.OtherSettings);
            SubscribeDevices(_recipe?.Devices);
        }

        private void UnsubscribeRecipe()
        {
            if (_recipe is INotifyPropertyChanged recipeNotify)
            {
                recipeNotify.PropertyChanged -= Recipe_PropertyChanged;
            }

            UnsubscribeOtherSettings();
            UnsubscribeDevices();
        }

        private void Recipe_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_recipe == null)
            {
                return;
            }

            if (e.PropertyName == nameof(MeasurementRecipe.OtherSettings))
            {
                UnsubscribeOtherSettings();
                SubscribeOtherSettings(_recipe.OtherSettings);
                ApplyMonitorState();
            }
            else if (e.PropertyName == nameof(MeasurementRecipe.Devices))
            {
                UnsubscribeDevices();
                SubscribeDevices(_recipe.Devices);
                InitializeConfiguredStepOperations();
            }
        }

        private void SubscribeOtherSettings(RecipeOtherSettingsConfig? settings)
        {
            _observedSettings = settings;
            if (_observedSettings != null)
            {
                _observedSettings.PropertyChanged += OtherSettings_PropertyChanged;
                SubscribeStepOperationBindings(_observedSettings.StepOperationBindings);
            }
        }

        private void UnsubscribeOtherSettings()
        {
            if (_observedSettings == null)
            {
                return;
            }

            _observedSettings.PropertyChanged -= OtherSettings_PropertyChanged;
            UnsubscribeStepOperationBindings();
            _observedSettings = null;
        }

        private void OtherSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RecipeOtherSettingsConfig.EnableStepOperationBinding))
            {
                ApplyMonitorState();
            }
            else if (e.PropertyName == nameof(RecipeOtherSettingsConfig.StepOperationBindings) && _observedSettings != null)
            {
                UnsubscribeStepOperationBindings();
                SubscribeStepOperationBindings(_observedSettings.StepOperationBindings);
                InitializeConfiguredStepOperations();
            }
        }

        private void SubscribeStepOperationBindings(ObservableCollection<StepOperationBindingConfig>? bindings)
        {
            _observedBindings = bindings;
            if (_observedBindings == null)
            {
                return;
            }

            _observedBindings.CollectionChanged += StepOperationBindings_CollectionChanged;
            foreach (var binding in _observedBindings)
            {
                binding.PropertyChanged += StepOperationBinding_PropertyChanged;
            }
        }

        private void UnsubscribeStepOperationBindings()
        {
            if (_observedBindings == null)
            {
                return;
            }

            _observedBindings.CollectionChanged -= StepOperationBindings_CollectionChanged;
            foreach (var binding in _observedBindings)
            {
                binding.PropertyChanged -= StepOperationBinding_PropertyChanged;
            }

            _observedBindings = null;
        }

        private void StepOperationBindings_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (StepOperationBindingConfig binding in e.OldItems)
                {
                    binding.PropertyChanged -= StepOperationBinding_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (StepOperationBindingConfig binding in e.NewItems)
                {
                    binding.PropertyChanged += StepOperationBinding_PropertyChanged;
                }
            }

            InitializeConfiguredStepOperations();
        }

        private void StepOperationBinding_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(StepOperationBindingConfig.PlcDeviceId) or nameof(StepOperationBindingConfig.DataPointId))
            {
                InitializeConfiguredStepOperations();
            }
        }

        private void SubscribeDevices(ObservableCollection<PlcDevice>? devices)
        {
            _observedDevices = devices;
            if (_observedDevices == null)
            {
                return;
            }

            _observedDevices.CollectionChanged += Devices_CollectionChanged;
            foreach (var device in _observedDevices)
            {
                SubscribeDevice(device);
            }
        }

        private void UnsubscribeDevices()
        {
            if (_observedDevices == null)
            {
                return;
            }

            _observedDevices.CollectionChanged -= Devices_CollectionChanged;
            foreach (var device in _observedDevices)
            {
                UnsubscribeDevice(device);
            }

            _observedDevices = null;
        }

        private void Devices_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (PlcDevice device in e.OldItems)
                {
                    UnsubscribeDevice(device);
                }
            }

            if (e.NewItems != null)
            {
                foreach (PlcDevice device in e.NewItems)
                {
                    SubscribeDevice(device);
                }
            }

            InitializeConfiguredStepOperations();
        }

        private void SubscribeDevice(PlcDevice device)
        {
            device.PropertyChanged += Device_PropertyChanged;
            device.DataPoints.CollectionChanged += DataPoints_CollectionChanged;

            foreach (var dataPoint in device.DataPoints)
            {
                dataPoint.PropertyChanged += DataPoint_PropertyChanged;
            }
        }

        private void UnsubscribeDevice(PlcDevice device)
        {
            device.PropertyChanged -= Device_PropertyChanged;
            device.DataPoints.CollectionChanged -= DataPoints_CollectionChanged;

            foreach (var dataPoint in device.DataPoints)
            {
                dataPoint.PropertyChanged -= DataPoint_PropertyChanged;
            }
        }

        private void Device_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(PlcDevice.IsEnabled) or nameof(PlcDevice.DeviceId))
            {
                InitializeConfiguredStepOperations();
            }
        }

        private void DataPoints_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (DataPoint dataPoint in e.OldItems)
                {
                    dataPoint.PropertyChanged -= DataPoint_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (DataPoint dataPoint in e.NewItems)
                {
                    dataPoint.PropertyChanged += DataPoint_PropertyChanged;
                }
            }

            InitializeConfiguredStepOperations();
        }

        private void DataPoint_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(DataPoint.IsEnabled) or nameof(DataPoint.PointId))
            {
                InitializeConfiguredStepOperations();
            }
        }

        private void ApplyMonitorState()
        {
            if (_recipe?.OtherSettings.EnableStepOperationBinding == true)
            {
                InitializeConfiguredStepOperations();
                StartMonitor();
            }
            else
            {
                StopMonitor();
            }
        }

        private void StartMonitor()
        {
            lock (_syncRoot)
            {
                if (_monitorCts != null || _recipe == null)
                {
                    return;
                }

                _monitorCts = new CancellationTokenSource();
                _ = MonitorStepOperationsAsync(_monitorCts.Token);
            }
        }

        private void StopMonitor()
        {
            CancellationTokenSource? monitorCts;
            lock (_syncRoot)
            {
                monitorCts = _monitorCts;
                _monitorCts = null;
            }

            if (monitorCts == null)
            {
                return;
            }

            monitorCts.Cancel();
            monitorCts.Dispose();
            ResetObservedStepOperations(_recipe?.OtherSettings?.StepOperationBindings);
        }

        private async Task MonitorStepOperationsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    CheckConfiguredStepOperations();
                    await Task.Delay(_delayTime, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _log.Error($"工步操作监听异常: {ex.Message}");
                    await Task.Delay(_delayTime, cancellationToken);
                }
            }
        }

        private void CheckConfiguredStepOperations()
        {
            var recipe = _recipe;
            if (recipe?.OtherSettings == null || !recipe.OtherSettings.EnableStepOperationBinding)
            {
                ResetObservedStepOperations(recipe?.OtherSettings?.StepOperationBindings);
                return;
            }

            foreach (var binding in recipe.OtherSettings.StepOperationBindings
                .Where(b => b.IsEnabled)
                .OrderBy(GetStepOperationPriority))
            {
                if (!TryGetStepOperationValue(binding, out var currentValue))
                {
                    binding.ResetObservedValue();
                    continue;
                }

                if (!ShouldTriggerStepOperation(binding, currentValue))
                {
                    continue;
                }

                _log.Info($"点位触发工步操作：{binding.OperationType.GetDescription()}");
                OperationTriggered?.Invoke(this, new StepOperationTriggeredEventArgs(binding.OperationType));
                break;
            }
        }

        /// <summary>
        /// 监听启动前按当前配方初始化一次工步绑定运行时引用。
        /// 轮询阶段只读点位值，不再反复刷新绑定源，避免界面编辑时闪烁。
        /// </summary>
        private void InitializeConfiguredStepOperations()
        {
            if (_isRefreshingBindings)
            {
                return;
            }

            var recipe = _recipe;
            if (recipe?.OtherSettings == null)
            {
                return;
            }

            try
            {
                _isRefreshingBindings = true;
                recipe.OtherSettings.HydrateStepOperationBindings(recipe.Devices);
            }
            finally
            {
                _isRefreshingBindings = false;
            }
        }

        /// <summary>
        /// 获取点位触发操作优先级。
        /// 停止优先级最高，避免多个点位同拍触发时抢占顺序不稳定。
        /// </summary>
        private static int GetStepOperationPriority(StepOperationBindingConfig binding)
        {
            return binding.OperationType switch
            {
                StepOperationType.TerminateMeasurement => 0,
                StepOperationType.StopAcquisition => 1,
                StepOperationType.PreviousStep => 2,
                StepOperationType.NextStep => 3,
                StepOperationType.StartAcquisition => 4,
                _ => int.MaxValue
            };
        }

        /// <summary>
        /// 当监听关闭或点位状态失效时，清空历史观测值，避免旧值影响下一次触发。
        /// </summary>
        private static void ResetObservedStepOperations(IEnumerable<StepOperationBindingConfig>? bindings)
        {
            if (bindings == null)
            {
                return;
            }

            foreach (var binding in bindings)
            {
                binding.ResetObservedValue();
            }
        }

        /// <summary>
        /// 尝试读取绑定点位当前值。
        /// 只有设备、点位、连接状态都正常时才返回成功。
        /// </summary>
        private static bool TryGetStepOperationValue(StepOperationBindingConfig binding, out object? currentValue)
        {
            currentValue = null;

            var dataPoint = binding.RuntimeDataPoint;
            if (binding.RuntimeDevice == null || dataPoint == null)
            {
                return false;
            }

            if (!binding.RuntimeDevice.IsEnabled || !binding.RuntimeDevice.IsConnected || !dataPoint.IsEnabled || !dataPoint.IsSuccess)
            {
                return false;
            }

            currentValue = dataPoint.CurrentValue;
            return currentValue != null;
        }

        /// <summary>
        /// 根据触发模式判断当前是否应抛出动作事件。
        /// </summary>
        private static bool ShouldTriggerStepOperation(StepOperationBindingConfig binding, object? currentValue)
        {
            var hasPreviousValue = binding.HasObservedValue;
            var previousValue = binding.LastObservedValue;

            binding.LastObservedValue = currentValue;
            binding.HasObservedValue = true;

            if (!hasPreviousValue)
            {
                return false;
            }

            return binding.TriggerMode switch
            {
                StepOperationTriggerMode.ValueEquals => !MatchesConfiguredValue(previousValue, binding.TriggerValue) && MatchesConfiguredValue(currentValue, binding.TriggerValue),
                StepOperationTriggerMode.RisingEdge => TryConvertToBool(previousValue, out var oldBool) && TryConvertToBool(currentValue, out var newBool) && !oldBool && newBool,
                StepOperationTriggerMode.FallingEdge => TryConvertToBool(previousValue, out var previousBool) && TryConvertToBool(currentValue, out var currentBool) && previousBool && !currentBool,
                StepOperationTriggerMode.AnyChange => !AreEquivalentValues(previousValue, currentValue),
                _ => false
            };
        }

        /// <summary>
        /// 判断当前值是否与配置值相等。
        /// </summary>
        private static bool MatchesConfiguredValue(object? value, string configuredValue)
        {
            if (value == null)
            {
                return false;
            }

            if (value is bool booleanValue)
            {
                return TryConvertToBool(configuredValue, out var parsedBool) && booleanValue == parsedBool;
            }

            if (TryConvertToDecimal(value, out var currentDecimal) && decimal.TryParse(configuredValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var configuredDecimal))
            {
                return currentDecimal == configuredDecimal;
            }

            return string.Equals(Convert.ToString(value, CultureInfo.InvariantCulture), configuredValue, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 比较两个值在业务上是否等价。
        /// </summary>
        private static bool AreEquivalentValues(object? left, object? right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            if (TryConvertToDecimal(left, out var leftDecimal) && TryConvertToDecimal(right, out var rightDecimal))
            {
                return leftDecimal == rightDecimal;
            }

            return Equals(left, right);
        }

        /// <summary>
        /// 将对象尽量转换为布尔值。
        /// </summary>
        private static bool TryConvertToBool(object? value, out bool result)
        {
            switch (value)
            {
                case bool booleanValue:
                    result = booleanValue;
                    return true;
                case string stringValue:
                    if (bool.TryParse(stringValue, out result))
                    {
                        return true;
                    }

                    if (stringValue == "1")
                    {
                        result = true;
                        return true;
                    }

                    if (stringValue == "0")
                    {
                        result = false;
                        return true;
                    }
                    break;
            }

            if (TryConvertToDecimal(value, out var numericValue))
            {
                result = numericValue != 0;
                return true;
            }

            result = false;
            return false;
        }

        /// <summary>
        /// 将对象尽量转换为 decimal，用于统一数值比较。
        /// </summary>
        private static bool TryConvertToDecimal(object? value, out decimal result)
        {
            try
            {
                if (value == null)
                {
                    result = 0;
                    return false;
                }

                result = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                result = 0;
                return false;
            }
        }
    }
}
