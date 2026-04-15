using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Config;
using MeasurementSoftware.Services.Devices;

namespace MeasurementSoftware.Services.Measurements
{
    /// <summary>
    /// 间接测量处理器。
    /// </summary>
    public sealed class IndirectMeasurementChannelHandler : MeasurementChannelHandlerBase
    {
        private readonly IMeasurementFormulaScriptEvaluator _formulaScriptEvaluator;
        private readonly Dictionary<MeasurementChannel, Dictionary<string, double>> _lastCalculatedSourceValues = new();

        public IndirectMeasurementChannelHandler(IMeasurementFormulaScriptEvaluator formulaScriptEvaluator)
        {
            _formulaScriptEvaluator = formulaScriptEvaluator;
        }

        public override MeasurementChannelMode Mode => MeasurementChannelMode.Indirect;

        public override void InitializeNewChannel(MeasurementChannel channel, IReadOnlyList<PlcDevice> enabledDevices)
        {
            channel.EnsureIndirectSourceBindings(1);
            foreach (var binding in channel.IndirectSourceBindings.Where(b => b.RuntimeDevice == null))
            {
                binding.RuntimeDevice = enabledDevices.FirstOrDefault();
            }
        }

        public override void HydrateBindings(MeasurementChannel channel, IDeviceConfigService deviceConfigService)
        {
            channel.EnsureIndirectSourceBindings(1);
            foreach (var binding in channel.IndirectSourceBindings)
            {
                if (binding.PlcDeviceId == 0)
                {
                    binding.ClearRuntimeBindings();
                    continue;
                }

                var device = FindDevice(deviceConfigService.Devices, binding.PlcDeviceId);
                if (device?.IsEnabled != true)
                {
                    binding.ClearRuntimeBindings();
                    continue;
                }

                binding.HydrateRuntimeBindings(device);
            }
        }

        public override void SyncBindings(MeasurementChannel channel, IDeviceConfigService deviceConfigService)
        {
            channel.EnsureIndirectSourceBindings(1);
            foreach (var binding in channel.IndirectSourceBindings)
            {
                if (binding.RuntimeDevice != null)
                {
                    binding.PlcDeviceId = binding.RuntimeDevice.DeviceId;
                }

                if (binding.RuntimeDataPoint != null)
                {
                    binding.DataPointId = binding.RuntimeDataPoint.PointId;
                    binding.DataSourceAddress = binding.RuntimeDataPoint.Address;
                }
                else if (binding.PlcDeviceId == 0)
                {
                    binding.DataPointId = string.Empty;
                    binding.DataSourceAddress = string.Empty;
                }

                if (binding.PlcDeviceId != 0)
                {
                    binding.HydrateRuntimeBindings(FindDevice(deviceConfigService.Devices, binding.PlcDeviceId));
                }
            }
        }

        public override bool ValidateConfiguration(MeasurementChannel channel, out string errorMessage)
        {
            if (channel.IndirectSourceBindings.Count == 0)
            {
                errorMessage = "间接测量至少需要配置一个数据源";
                return false;
            }

            var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var variables = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var binding in channel.IndirectSourceBindings)
            {
                var alias = binding.SourceKey?.Trim();
                if (string.IsNullOrWhiteSpace(alias))
                {
                    errorMessage = "间接测量的数据源变量名不能为空";
                    return false;
                }

                if (!IsValidAlias(alias))
                {
                    errorMessage = $"变量名 {alias} 只能包含字母、数字和下划线，且必须以字母或下划线开头";
                    return false;
                }

                if (!aliases.Add(alias))
                {
                    errorMessage = $"变量名 {alias} 重复，请修改后重试";
                    return false;
                }

                if (binding.RuntimeDevice == null || binding.RuntimeDataPoint == null)
                {
                    errorMessage = $"变量 {alias} 必须绑定设备和数据点位";
                    return false;
                }

                variables[alias] = 1d;
            }

            if (string.IsNullOrWhiteSpace(channel.IndirectFormula))
            {
                errorMessage = "间接测量公式脚本不能为空";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        public override bool TryHandleDataPointUpdates(MeasurementChannel channel, PlcDataPointsUpdatedEventArgs e)
        {
            var affectedBindings = channel.IndirectSourceBindings.Where(binding =>
                ReferenceEquals(binding.RuntimeDevice, e.Device)
                && binding.RuntimeDataPoint != null
                && e.DataPoints.Contains(binding.RuntimeDataPoint)).ToList();

            if (affectedBindings.Count == 0)
            {
                return false;
            }

            if (!TryBuildCurrentVariables(channel, out var currentVariables))
            {
                return false;
            }

            if (!ShouldTriggerCalculation(channel, currentVariables))
            {
                return false;
            }

            return TryEvaluateAndUpdateChannel(channel, currentVariables);
        }

        public override bool TryHandleCacheFieldUpdates(MeasurementChannel channel, PlcCacheFieldsUpdatedEventArgs e)
        {
            var updatedKeys = e.Updates
                .Where(update => !string.IsNullOrWhiteSpace(update.CacheFieldKey))
                .Select(update => update.CacheFieldKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var affectedBindings = channel.IndirectSourceBindings.Where(binding =>
                ReferenceEquals(binding.RuntimeDevice, e.Device)
                && !string.IsNullOrWhiteSpace(binding.RuntimeDataPoint?.CacheFieldKey)
                && updatedKeys.Contains(binding.RuntimeDataPoint.CacheFieldKey)).ToList();

            if (affectedBindings.Count == 0)
            {
                return false;
            }

            if (!TryBuildCurrentVariables(channel, out var currentVariables))
            {
                return false;
            }

            if (!ShouldTriggerCalculation(channel, currentVariables))
            {
                return false;
            }

            return TryEvaluateAndUpdateChannel(channel, currentVariables);
        }

        public override bool TryHandleConnectionStateChanged(MeasurementChannel channel, PlcDeviceConnectionChangedEventArgs e)
        {
            if (!channel.IndirectSourceBindings.Any(binding => ReferenceEquals(binding.RuntimeDevice, e.Device)))
            {
                return false;
            }

            if (!e.IsConnected)
            {
                channel.ChannelDescription = $"间接数据源设备 {e.Device.DeviceName} 未连接";
                channel.DisplayState = MeasurementResult.Waiting;
                return true;
            }

            if (TryBuildCurrentVariables(channel, out var currentVariables) && TryEvaluateAndUpdateChannel(channel, currentVariables))
            {
                return true;
            }

            channel.DisplayState = MeasurementResult.Waiting;
            return true;
        }

        public override void ResetRuntimeState(MeasurementChannel channel)
        {
            _lastCalculatedSourceValues.Remove(channel);
        }

        private bool TryEvaluateAndUpdateChannel(MeasurementChannel channel, Dictionary<string, double> variables)
        {
            if (!_formulaScriptEvaluator.TryEvaluateScript(channel.IndirectFormula, variables, out var formulaValue, out _, out _, out var errorMessage))
            {
                channel.ChannelDescription = errorMessage;
                channel.DisplayState = MeasurementResult.Waiting;
                return false;
            }

            channel.ChannelDescription = string.Empty;
            channel.UpdateMeasuredValue(formulaValue);
            channel.DisplayState = MeasurementResult.Acquiring;
            _lastCalculatedSourceValues[channel] = new Dictionary<string, double>(variables, StringComparer.OrdinalIgnoreCase);
            return true;
        }

        private bool TryBuildCurrentVariables(MeasurementChannel channel, out Dictionary<string, double> variables)
        {
            variables = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var binding in channel.IndirectSourceBindings)
            {
                var sourceKey = binding.SourceKey?.Trim();
                if (string.IsNullOrWhiteSpace(sourceKey))
                {
                    continue;
                }

                if (!TryGetBindingCurrentValue(channel, binding, out var currentValue))
                {
                    channel.DisplayState = MeasurementResult.Waiting;
                    return false;
                }

                variables[sourceKey] = currentValue;
            }

            return true;
        }

        private bool ShouldTriggerCalculation(MeasurementChannel channel, IReadOnlyDictionary<string, double> currentVariables)
        {
            if (!_lastCalculatedSourceValues.TryGetValue(channel, out var lastCalculatedVariables) || lastCalculatedVariables.Count == 0)
            {
                return true;
            }

            switch (channel.IndirectTriggerMode)
            {
                case IndirectMeasurementTriggerMode.EventReceived:
                    return true;

                case IndirectMeasurementTriggerMode.AnyValueChanged:
                    return currentVariables.Any(pair => !lastCalculatedVariables.TryGetValue(pair.Key, out var lastValue) || Math.Abs(lastValue - pair.Value) > double.Epsilon);

                case IndirectMeasurementTriggerMode.AllValuesChanged:
                    return currentVariables.All(pair => !lastCalculatedVariables.TryGetValue(pair.Key, out var lastValue) || Math.Abs(lastValue - pair.Value) > double.Epsilon);

                default:
                    return false;
            }
        }

        private static bool IsValidAlias(string alias)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                return false;
            }

            if (!(char.IsLetter(alias[0]) || alias[0] == '_'))
            {
                return false;
            }

            return alias.All(ch => char.IsLetterOrDigit(ch) || ch == '_');
        }
    }
}
