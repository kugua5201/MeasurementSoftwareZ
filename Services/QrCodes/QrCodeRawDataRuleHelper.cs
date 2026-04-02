using MeasurementSoftware.Models;
using System.Text;

namespace MeasurementSoftware.Services.QrCodes
{
    internal static class QrCodeRawDataRuleHelper
    {
        public static bool TryValidate(QrCodeConfig config, out string error)
        {
            error = string.Empty;

            if (config.EnableStartSymbol && string.IsNullOrEmpty(config.StartSymbol))
            {
                error = "已启用起始符校验，请填写起始符";
                return false;
            }

            if (config.EnableEndSymbol && string.IsNullOrEmpty(config.EndSymbol))
            {
                error = "已启用结束符校验，请填写结束符";
                return false;
            }

            if (config.EnableLengthCheck && config.ExpectedLength <= 0)
            {
                error = "已启用长度校验，期望长度必须大于0";
                return false;
            }

            if (config.EnableLengthCheck && config.QrCodeStartIndex + config.QrCodeLength > config.ExpectedLength)
            {
                error = "二维码提取范围超出了期望长度，请检查起始索引和提取长度配置";
                return false;
            }

            return true;
        }

        public static bool TryResolveStartSymbol(QrCodeConfig config, out string symbol, out string error)
        {
            symbol = config.EnableStartSymbol ? NormalizeRuleText(config.StartSymbol) : string.Empty;
            error = string.Empty;

            if (config.EnableStartSymbol && string.IsNullOrEmpty(symbol))
            {
                error = "已启用起始符校验，请填写起始符";
                return false;
            }

            return true;
        }

        public static bool TryResolveEndSymbol(QrCodeConfig config, out string symbol, out string error)
        {
            symbol = config.EnableEndSymbol ? NormalizeRuleText(config.EndSymbol) : string.Empty;
            error = string.Empty;

            if (config.EnableEndSymbol && string.IsNullOrEmpty(symbol))
            {
                error = "已启用结束符校验，请填写结束符";
                return false;
            }

            return true;
        }

        public static bool TryConsume(StringBuilder buffer, QrCodeConfig config, out string rawData)
        {
            rawData = string.Empty;
            if (buffer.Length == 0)
            {
                return false;
            }

            if (!TryResolveStartSymbol(config, out var startSymbol, out _))
            {
                return false;
            }

            if (!TryResolveEndSymbol(config, out var endSymbol, out _))
            {
                return false;
            }

            var content = buffer.ToString();
            var startIndex = 0;

            if (!string.IsNullOrEmpty(startSymbol))
            {
                startIndex = content.IndexOf(startSymbol, StringComparison.Ordinal);
                if (startIndex < 0)
                {
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(endSymbol))
            {
                var endIndex = content.IndexOf(endSymbol, startIndex + startSymbol.Length, StringComparison.Ordinal);
                if (endIndex < 0)
                {
                    if (startIndex > 0)
                    {
                        buffer.Remove(0, startIndex);
                    }

                    return false;
                }

                var totalLength = endIndex + endSymbol.Length - startIndex;
                rawData = content.Substring(startIndex, totalLength);
                buffer.Remove(0, endIndex + endSymbol.Length);
                return rawData.Length > 0;
            }

            if (config.EnableLengthCheck)
            {
                var availableLength = content.Length - startIndex;
                if (availableLength < config.ExpectedLength)
                {
                    if (startIndex > 0)
                    {
                        buffer.Remove(0, startIndex);
                    }

                    return false;
                }

                rawData = content.Substring(startIndex, config.ExpectedLength);
                buffer.Remove(0, startIndex + config.ExpectedLength);
                return rawData.Length > 0;
            }
            rawData = content;
            return true;
        }

        private static string NormalizeRuleText(string? ruleText)
        {
            return (ruleText ?? string.Empty)
                .Replace("\\r", "\r", StringComparison.Ordinal)
                .Replace("\\n", "\n", StringComparison.Ordinal)
                .Replace("\\t", "\t", StringComparison.Ordinal);
        }
    }
}