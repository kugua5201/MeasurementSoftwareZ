using Autofac;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Logs;
using System.Text;

namespace MeasurementSoftware.Services.QrCodes
{
    /// <summary>
    /// 二维码扫描服务。
    /// 统一收口配置校验、二维码提取以及不同扫码来源的调度逻辑。
    /// </summary>
    public class QrCodeScanService : IQrCodeScanService
    {
        private readonly IComponentContext _componentContext;
        private readonly IKeyboardQrCodeInputService _keyboardQrCodeInputService;
        private readonly ILog _log;

        public QrCodeScanService(IComponentContext componentContext, IKeyboardQrCodeInputService keyboardQrCodeInputService, ILog log)
        {
            _componentContext = componentContext;
            _keyboardQrCodeInputService = keyboardQrCodeInputService;
            _log = log;
        }

        public bool ValidateScanConfig(QrCodeConfig config, out string error)
        {
            error = string.Empty;

            if (!config.IsEnabled)
            {
                error = "当前未启用扫码功能";
                return false;
            }

            switch (config.SourceType)
            {
                case QrCodeSourceType.KeyboardInput:
                    break;

                case QrCodeSourceType.SerialPort:
                    if (string.IsNullOrWhiteSpace(config.SerialPortName))
                    {
                        error = "请选择串口";
                        return false;
                    }
                    break;

                case QrCodeSourceType.Ethernet:
                    if (string.IsNullOrWhiteSpace(config.EthernetIp))
                    {
                        error = "请输入以太网IP地址";
                        return false;
                    }

                    if (config.EthernetPort <= 0 || config.EthernetPort > 65535)
                    {
                        error = "以太网端口范围：1-65535";
                        return false;
                    }
                    break;

                case QrCodeSourceType.PlcRegister:
                    if (config.PlcDeviceId <= 0)
                    {
                        error = "请选择PLC设备";
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(config.Address))
                    {
                        error = "请选择点位地址";
                        return false;
                    }
                    break;
            }

            if (config.QrCodeStartIndex < 0)
            {
                error = "起始索引不能为负数";
                return false;
            }

            if (config.QrCodeLength <= 0)
            {
                error = "提取长度必须大于0";
                return false;
            }

            if (!QrCodeRawDataRuleHelper.TryValidate(config, out error))
            {
                return false;
            }

            return true;
        }

        public bool TryExtractQrCode(QrCodeConfig config, string rawData, out string extractedQrCode, out string validationMessage)
        {
            extractedQrCode = string.Empty;
            var builder = new StringBuilder();
            rawData ??= string.Empty;

            if (!QrCodeRawDataRuleHelper.TryValidate(config, out validationMessage))
            {
                SetRuntimeValidationState(config, false, BuildDiagnosticMessage(rawData, validationMessage), validationMessage);
                return false;
            }

            QrCodeRawDataRuleHelper.TryResolveStartSymbol(config, out var startSymbol, out _);
            QrCodeRawDataRuleHelper.TryResolveEndSymbol(config, out var endSymbol, out _);

            if (!string.IsNullOrEmpty(startSymbol))
            {
                if (!rawData.StartsWith(startSymbol, StringComparison.Ordinal))
                {
                    builder.AppendLine($"❌ 起始符校验失败：期望'{config.StartSymbol}'");
                    validationMessage = builder.ToString();
                    SetRuntimeValidationState(config, false, BuildDiagnosticMessage(rawData, validationMessage), validationMessage);
                    return false;
                }

                builder.AppendLine($"✅ 起始符校验通过：'{config.StartSymbol}'");
            }

            if (!string.IsNullOrEmpty(endSymbol))
            {
                if (!rawData.EndsWith(endSymbol, StringComparison.Ordinal))
                {
                    builder.AppendLine($"❌ 结束符校验失败：期望'{config.EndSymbol}'");
                    validationMessage = builder.ToString();
                    SetRuntimeValidationState(config, false, BuildDiagnosticMessage(rawData, validationMessage), validationMessage);
                    return false;
                }

                builder.AppendLine($"✅ 结束符校验通过：'{config.EndSymbol}'");
            }

            var payload = rawData;
            if (!string.IsNullOrEmpty(startSymbol) && payload.StartsWith(startSymbol, StringComparison.Ordinal))
            {
                payload = payload[startSymbol.Length..];
            }

            if (!string.IsNullOrEmpty(endSymbol) && payload.EndsWith(endSymbol, StringComparison.Ordinal))
            {
                payload = payload[..^endSymbol.Length];
            }

            if (config.EnableLengthCheck)
            {
                if (payload.Length != config.ExpectedLength)
                {
                    builder.AppendLine($"❌ 长度校验失败：期望{config.ExpectedLength}，实际{payload.Length}");
                    validationMessage = builder.ToString();
                    SetRuntimeValidationState(config, false, BuildDiagnosticMessage(rawData, validationMessage), validationMessage);
                    return false;
                }

                builder.AppendLine($"✅ 长度校验通过：{payload.Length}");
            }

            if (config.QrCodeStartIndex < 0 || config.QrCodeStartIndex >= payload.Length)
            {
                builder.AppendLine($"❌ 起始索引超出范围：{config.QrCodeStartIndex}");
                validationMessage = builder.ToString();
                SetRuntimeValidationState(config, false, BuildDiagnosticMessage(rawData, validationMessage), validationMessage);
                return false;
            }

            var endIndex = config.QrCodeStartIndex + config.QrCodeLength;
            if (endIndex > payload.Length)
            {
                builder.AppendLine($"❌ 提取长度超出范围：起始{config.QrCodeStartIndex}，长度{config.QrCodeLength}");
                validationMessage = builder.ToString();
                SetRuntimeValidationState(config, false, BuildDiagnosticMessage(rawData, validationMessage), validationMessage);
                return false;
            }

            extractedQrCode = payload.Substring(config.QrCodeStartIndex, config.QrCodeLength);
            builder.AppendLine($"✅ 成功提取二维码：{extractedQrCode}");
            validationMessage = builder.ToString();
            SetRuntimeValidationState(config, true, extractedQrCode, validationMessage);
            return true;
        }

        public async Task<(bool Success, string RawData, string ExtractedQrCode, string Message)> ReceiveAndValidateOnceAsync(QrCodeConfig config, CancellationToken cancellationToken)
        {
            if (!ValidateScanConfig(config, out var error))
            {
                SetRuntimeValidationState(config, false, error, error);
                return (false, string.Empty, string.Empty, error);
            }

            var handler = CreateHandler(config.SourceType);

            if (config.SourceType == QrCodeSourceType.KeyboardInput)
            {
                _keyboardQrCodeInputService.ClearPending();
            }

            var rawData = await handler.WaitForRawDataAsync(config, cancellationToken) ?? string.Empty;
            if (string.IsNullOrEmpty(rawData))
            {
                const string emptyMessage = "未接收到二维码数据";
                SetRuntimeValidationState(config, false, emptyMessage, emptyMessage);
                return (false, string.Empty, string.Empty, emptyMessage);
            }

            if (TryExtractQrCode(config, rawData, out var extractedQrCode, out var validationMessage))
            {
                return (true, rawData, extractedQrCode, BuildDiagnosticMessage(rawData, validationMessage));
            }

            return (false, rawData, string.Empty, BuildDiagnosticMessage(rawData, validationMessage));
        }

        public async Task<string> WaitForQrCodeAsync(QrCodeConfig config, CancellationToken cancellationToken)
        {
            if (!ValidateScanConfig(config, out var error))
            {
                throw new InvalidOperationException(error);
            }

            var handler = CreateHandler(config.SourceType);

            if (config.SourceType == QrCodeSourceType.KeyboardInput)
            {
                _keyboardQrCodeInputService.ClearPending();
            }

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var rawData = await handler.WaitForRawDataAsync(config, cancellationToken) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(rawData))
                {
                    continue;
                }

                if (TryExtractQrCode(config, rawData, out var extractedQrCode, out var validationMessage))
                {
                    return extractedQrCode;
                }

                _log.Warn($"扫码数据校验失败，继续等待下一次扫码：{validationMessage.Replace(Environment.NewLine, " | ")}");
            }
        }

        private static void SetRuntimeValidationState(QrCodeConfig config, bool passed, string displayText, string validationMessage)
        {
            config.RuntimeValidationPassed = passed;
            config.RuntimeDisplayText = string.IsNullOrWhiteSpace(displayText) ? (passed ? "扫码成功" : "扫码失败") : displayText;
            config.RuntimeValidationMessage = validationMessage;
        }

        private IQrCodeSourceHandler CreateHandler(QrCodeSourceType sourceType)
        {
            return sourceType switch
            {
                QrCodeSourceType.KeyboardInput => _componentContext.Resolve<KeyboardInputQrCodeSourceHandler>(),
                QrCodeSourceType.SerialPort => _componentContext.Resolve<SerialPortQrCodeSourceHandler>(),
                QrCodeSourceType.Ethernet => _componentContext.Resolve<EthernetQrCodeSourceHandler>(),
                QrCodeSourceType.PlcRegister => _componentContext.Resolve<PlcRegisterQrCodeSourceHandler>(),
                _ => throw new NotSupportedException($"暂不支持的数据源类型：{sourceType}")
            };
        }

        private static string BuildDiagnosticMessage(string rawData, string validationMessage)
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrEmpty(rawData))
            {
                builder.AppendLine($"接收值：{ToDisplayText(rawData)}");
            }

            if (!string.IsNullOrWhiteSpace(validationMessage))
            {
                builder.Append(validationMessage.TrimEnd());
            }

            return builder.ToString();
        }

        private static string ToDisplayText(string value)
        {
            return value
                .Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal)
                .Replace("\t", "\\t", StringComparison.Ordinal);
        }

        public string GenerateBatchNumber(QrCodeConfig config)
        {
            var currentDate = DateTime.Now.ToString(config.BatchDateFormat);

            if (config.LastBatchDate != currentDate)
            {
                config.LastBatchDate = currentDate;
                config.CurrentBatchSerial = 1;
            }

            var serialNumber = config.CurrentBatchSerial.ToString($"D{config.BatchSerialDigits}");
            var batchNumber = $"{config.BatchPrefix}{currentDate}{serialNumber}";
            config.CurrentBatchSerial++;
            return batchNumber;
        }
    }
}
