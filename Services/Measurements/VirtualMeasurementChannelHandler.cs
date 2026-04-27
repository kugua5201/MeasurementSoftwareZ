using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Config;
using MeasurementSoftware.Services.Devices;

namespace MeasurementSoftware.Services.Measurements
{
    /// <summary>
    /// 虚拟通道处理器。
    /// 当前仅保留展示与占位，不参与采集。
    /// </summary>
    public sealed class VirtualMeasurementChannelHandler : MeasurementChannelHandlerBase
    {
        private readonly IRecipeConfigService _recipeConfigService;
        private readonly IMeasurementFormulaScriptEvaluator _formulaScriptEvaluator;
        private readonly Dictionary<MeasurementChannel, DateTime> _waveformStartTimes = new();

        public VirtualMeasurementChannelHandler(IRecipeConfigService recipeConfigService, IMeasurementFormulaScriptEvaluator formulaScriptEvaluator)
        {
            _recipeConfigService = recipeConfigService;
            _formulaScriptEvaluator = formulaScriptEvaluator;
        }

        public override MeasurementChannelMode Mode => MeasurementChannelMode.Virtual;

        public override void InitializeNewChannel(MeasurementChannel channel, IReadOnlyList<PlcDevice> enabledDevices)
        {
            channel.ChannelDescription = string.Empty;
            channel.EnsureVirtualSourceBindings(1);
        }

        public override void HydrateBindings(MeasurementChannel channel, IDeviceConfigService deviceConfigService)
        {
            channel.EnsureVirtualSourceBindings(1);
            HydrateVirtualSourceBindings(channel);
            channel.ChannelDescription = string.Empty;
        }

        public override void SyncBindings(MeasurementChannel channel, IDeviceConfigService deviceConfigService)
        {
            channel.EnsureVirtualSourceBindings(1);
            foreach (var binding in channel.VirtualSourceBindings)
            {
                if (binding.RuntimeChannel != null)
                {
                    binding.SourceChannelNumber = binding.RuntimeChannel.ChannelNumber;
                }
            }
        }

        public override bool ValidateConfiguration(MeasurementChannel channel, out string errorMessage)
        {
            if (channel.VirtualSourceMode == VirtualMeasurementSourceMode.SoftwareSimulation)
            {
                if (channel.VirtualWaveformAmplitude <= 0)
                {
                    errorMessage = "软件模拟数据的幅值必须大于 0";
                    return false;
                }

                if (channel.VirtualWaveformPeriodSeconds <= 0)
                {
                    errorMessage = "软件模拟数据的周期必须大于 0 毫秒";
                    return false;
                }

                if (channel.VirtualWaveformType == VirtualMeasurementWaveformType.Square
                    && (channel.VirtualWaveformDutyCycle <= 0 || channel.VirtualWaveformDutyCycle >= 1))
                {
                    errorMessage = "方波占空比必须介于 0 和 1 之间";
                    return false;
                }

                errorMessage = string.Empty;
                return true;
            }

            if (string.IsNullOrWhiteSpace(channel.VirtualFormula))
            {
                errorMessage = "虚拟测量公式不能为空";
                return false;
            }

            var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var binding in channel.VirtualSourceBindings)
            {
                var alias = binding.SourceKey?.Trim();
                if (string.IsNullOrWhiteSpace(alias))
                {
                    errorMessage = "虚拟测量变量名不能为空";
                    return false;
                }

                if (!(char.IsLetter(alias[0]) || alias[0] == '_') || alias.Any(ch => !char.IsLetterOrDigit(ch) && ch != '_'))
                {
                    errorMessage = $"虚拟测量变量名 {alias} 只能包含字母、数字和下划线，且必须以字母或下划线开头";
                    return false;
                }

                if (!aliases.Add(alias))
                {
                    errorMessage = $"虚拟测量变量名 {alias} 重复，请修改后重试";
                    return false;
                }

                if (binding.RuntimeChannel == null)
                {
                    errorMessage = $"变量 {alias} 必须绑定来源通道";
                    return false;
                }

                if (binding.RuntimeChannel.ChannelNumber == channel.ChannelNumber)
                {
                    errorMessage = "虚拟测量不能引用自身通道作为公式来源";
                    return false;
                }
            }

            errorMessage = string.Empty;
            return true;
        }

        public override bool TryHandleDataPointUpdates(MeasurementChannel channel, PlcDataPointsUpdatedEventArgs e)
        {
            return channel.VirtualSourceMode == VirtualMeasurementSourceMode.SoftwareSimulation
                && TryGenerateWaveform(channel);
        }

        public override bool TryHandleCacheFieldUpdates(MeasurementChannel channel, PlcCacheFieldsUpdatedEventArgs e)
        {
            return channel.VirtualSourceMode == VirtualMeasurementSourceMode.SoftwareSimulation
                && TryGenerateWaveform(channel);
        }

        public override bool TryHandleConnectionStateChanged(MeasurementChannel channel, PlcDeviceConnectionChangedEventArgs e)
        {
            return channel.VirtualSourceMode == VirtualMeasurementSourceMode.SoftwareSimulation
                && TryGenerateWaveform(channel);
        }

        public override void ResetRuntimeState(MeasurementChannel channel)
        {
            _waveformStartTimes.Remove(channel);

            //if (channel.VirtualSourceMode == VirtualMeasurementSourceMode.ChannelFormula)
            //{
            //    channel.ChannelDescription = string.Empty;
            //    channel.IsMeasuredValueAvailable = false;
            //    channel.DisplayState = MeasurementResult.Waiting;
            //}
        }

        public bool TryEvaluateFinalResult(MeasurementChannel channel, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (channel.VirtualSourceMode != VirtualMeasurementSourceMode.ChannelFormula)
            {
                return false;
            }

            HydrateVirtualSourceBindings(channel);
            var variables = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var binding in channel.VirtualSourceBindings)
            {
                var alias = binding.SourceKey?.Trim();
                if (string.IsNullOrWhiteSpace(alias))
                {
                    continue;
                }

                var sourceChannel = binding.RuntimeChannel;
                if (sourceChannel == null)
                {
                    errorMessage = $"变量 {alias} 未绑定来源通道";
                    channel.ChannelDescription = errorMessage;
                    channel.IsMeasuredValueAvailable = false;
                    channel.DisplayState = MeasurementResult.Waiting;
                    return false;
                }

                if (!sourceChannel.IsEnabled)
                {
                    errorMessage = $"变量 {alias} 未启用";
                    channel.ChannelDescription = errorMessage;
                    channel.IsMeasuredValueAvailable = false;
                    channel.DisplayState = MeasurementResult.Waiting;
                    return false;
                }

                if (!sourceChannel.IsResultValueAvailable)
                {
                    errorMessage = $"变量 {alias} 的来源通道尚未产生最终结果";
                    channel.ChannelDescription = errorMessage;
                    channel.IsMeasuredValueAvailable = false;
                    channel.DisplayState = MeasurementResult.Waiting;
                    return false;
                }

                variables[alias] = sourceChannel.ReusltValue;
            }

            if (!_formulaScriptEvaluator.TryEvaluateScript(channel.VirtualFormula, variables, out var formulaValue, out _, out _, out errorMessage))
            {
                channel.ChannelDescription = errorMessage;
                channel.IsMeasuredValueAvailable = false;
                channel.DisplayState = MeasurementResult.Waiting;
                return false;
            }

            channel.ChannelDescription = string.Empty;
            channel.UpdateMeasuredValue(formulaValue);
            return true;
        }

        private void HydrateVirtualSourceBindings(MeasurementChannel channel)
        {
            var channels = _recipeConfigService.CurrentRecipe?.Channels ?? [];
            foreach (var binding in channel.VirtualSourceBindings)
            {
                binding.RuntimeChannel = channels.FirstOrDefault(item => item.ChannelNumber == binding.SourceChannelNumber);
            }
        }

        private bool TryGenerateWaveform(MeasurementChannel channel)
        {
            if (!_waveformStartTimes.TryGetValue(channel, out var startTime))
            {
                startTime = DateTime.UtcNow;
                _waveformStartTimes[channel] = startTime;
            }

            var elapsedMilliseconds = Math.Max(0d, (DateTime.UtcNow - startTime).TotalMilliseconds);
            var periodMilliseconds = Math.Max(channel.VirtualWaveformPeriodSeconds, 1d);
            var amplitude = channel.VirtualWaveformAmplitude;

            double value;
            if (channel.VirtualWaveformType == VirtualMeasurementWaveformType.Square)
            {
                var dutyCycle = Math.Clamp(channel.VirtualWaveformDutyCycle, 0.001d, 0.999d);
                var phase = (elapsedMilliseconds % periodMilliseconds) / periodMilliseconds;
                value = phase < dutyCycle ? amplitude : 0d;
            }
            else
            {
                value = amplitude * Math.Sin((2 * Math.PI * elapsedMilliseconds) / periodMilliseconds);
            }

            value += channel.VirtualWaveformOffset;

            channel.ChannelDescription = string.Empty;
            channel.UpdateMeasuredValue(value);
            channel.DisplayState = MeasurementResult.Acquiring;
            return true;
        }
    }
}
