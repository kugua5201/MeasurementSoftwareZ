using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services.QrCodes
{
    /// <summary>
    /// PLC寄存器扫码数据源处理器。
    /// 通过轮询当前绑定点位的值，等待本次测量启动后的新扫码数据。
    /// </summary>
    public class PlcRegisterQrCodeSourceHandler : IQrCodeSourceHandler
    {
        //private string _lastValue = string.Empty;

        //public QrCodeSourceType SourceType => QrCodeSourceType.PlcRegister;

        //public async Task<string?> WaitForRawDataAsync(QrCodeConfig config, CancellationToken cancellationToken)
        //{
        //    try
        //    {
        //        cancellationToken.ThrowIfCancellationRequested();

        //        var device = config.SelectedPlcDevice ?? throw new InvalidOperationException("当前PLC扫码设备未绑定或不存在");
        //        var point = config.SelectedPoint ?? throw new InvalidOperationException("当前PLC扫码点位未绑定或不存在");

        //        while (true)
        //        {
        //            cancellationToken.ThrowIfCancellationRequested();

        //            var currentValue = Normalize(point.CurrentValue);
        //            var normalizedValue = currentValue.Length <= config.PlcReadLength
        //                ? currentValue
        //                : currentValue[..config.PlcReadLength];

        //            if (point.IsSuccess && !string.IsNullOrWhiteSpace(normalizedValue))
        //            {
        //                if (string.IsNullOrWhiteSpace(_lastValue))
        //                {
        //                    _lastValue = normalizedValue;
        //                    return normalizedValue;
        //                }

        //                if (!string.Equals(normalizedValue, _lastValue, StringComparison.Ordinal))
        //                {
        //                    _lastValue = normalizedValue;
        //                    return normalizedValue;
        //                }
        //            }

        //            await Task.Delay(200, cancellationToken);
        //        }
        //    }
        //    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        //    {
        //        _lastValue = string.Empty;
        //        throw;
        //    }
        //}
        public QrCodeSourceType SourceType => QrCodeSourceType.PlcRegister;

        public async Task<string?> WaitForRawDataAsync(QrCodeConfig config, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = config.SelectedPlcDevice ?? throw new InvalidOperationException("当前PLC扫码设备未绑定或不存在");
            var point = config.SelectedPoint ?? throw new InvalidOperationException("当前PLC扫码点位未绑定或不存在");

            // 先获取一次初始值
            var currentValue = Normalize(point.CurrentValue);
            var baselineValue = currentValue.Length <= config.PlcReadLength
                ? currentValue
                : currentValue[..config.PlcReadLength];

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                currentValue = Normalize(point.CurrentValue);
                var normalizedValue = currentValue.Length <= config.PlcReadLength
                    ? currentValue
                    : currentValue[..config.PlcReadLength];

                if (point.IsSuccess && !string.IsNullOrWhiteSpace(normalizedValue)
                    && !string.Equals(normalizedValue, baselineValue, StringComparison.Ordinal))
                {
                    return normalizedValue;
                }

                await Task.Delay(200, cancellationToken);
            }
        }
        private static string Normalize(object? value)
        {
            return value?.ToString()?.Trim() ?? string.Empty;
        }
    }
}
