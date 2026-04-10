using MeasurementSoftware.Models;
using System.ComponentModel;

namespace MeasurementSoftware.Services.QrCodes
{
    /// <summary>
    /// PLC寄存器扫码数据源处理器。
    /// 通过点位更新事件等待本次测量启动后的新扫码数据。
    /// </summary>
    public class PlcRegisterQrCodeSourceHandler : IQrCodeSourceHandler
    {
        public QrCodeSourceType SourceType => QrCodeSourceType.PlcRegister;

        public async Task<string?> WaitForRawDataAsync(QrCodeConfig config, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = config.SelectedPlcDevice ?? throw new InvalidOperationException("当前PLC扫码设备未绑定或不存在");
            var point = config.SelectedPoint ?? throw new InvalidOperationException("当前PLC扫码点位未绑定或不存在");

            var currentValue = Normalize(point.CurrentValue);
            var baselineValue = currentValue.Length <= config.QrCodeLength
                ? currentValue
                : currentValue[..config.QrCodeLength];

            var completionSource = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

            PropertyChangedEventHandler? handler = null;
            CancellationTokenRegistration cancellationRegistration = default;

            bool TryResolveUpdatedQrCode()
            {
                var latestValue = Normalize(point.CurrentValue);
                var normalizedValue = latestValue.Length <= config.QrCodeLength
                    ? latestValue
                    : latestValue[..config.QrCodeLength];

                if (!point.IsSuccess || string.IsNullOrWhiteSpace(normalizedValue))
                {
                    return false;
                }

                if (string.Equals(normalizedValue, baselineValue, StringComparison.Ordinal))
                {
                    return false;
                }

                completionSource.TrySetResult(normalizedValue);
                return true;
            }

            handler = (_, e) =>
            {
                if (e.PropertyName is nameof(DataPoint.CurrentValue)
                    or nameof(DataPoint.IsSuccess)
                    or nameof(DataPoint.LastUpdateTime))
                {
                    if (TryResolveUpdatedQrCode())
                    {
                        point.PropertyChanged -= handler;
                        cancellationRegistration.Dispose();
                    }
                }
            };

            point.PropertyChanged += handler;
            cancellationRegistration = cancellationToken.Register(() =>
            {
                point.PropertyChanged -= handler;
                completionSource.TrySetCanceled(cancellationToken);
            });

            if (TryResolveUpdatedQrCode())
            {
                point.PropertyChanged -= handler;
                cancellationRegistration.Dispose();
            }

            try
            {
                return await completionSource.Task;
            }
            finally
            {
                point.PropertyChanged -= handler;
                cancellationRegistration.Dispose();
            }
        }

        private static string Normalize(object? value)
        {
            return value?.ToString()?.Trim() ?? string.Empty;
        }
    }
}
