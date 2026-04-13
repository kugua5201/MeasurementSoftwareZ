using MeasurementSoftware.Models;
using MeasurementSoftware.Helpers;
using System.IO.Ports;
using System.Text;
using System.Timers;

namespace MeasurementSoftware.Services.QrCodes
{
    /// <summary>
    /// 串口扫码数据源处理器。
    /// </summary>
    public class SerialPortQrCodeSourceHandler : IQrCodeSourceHandler
    {
        public QrCodeSourceType SourceType => QrCodeSourceType.SerialPort;

        public async Task<string?> WaitForRawDataAsync(QrCodeConfig config, CancellationToken cancellationToken)
        {
            if (!QrCodeRawDataRuleHelper.TryValidate(config, out var error))
            {
                throw new InvalidOperationException(error);
            }

            if (!SerialPortCompatibility.TryEnsureSupported(out var platformError))
            {
                throw new PlatformNotSupportedException(platformError);
            }

            try
            {
                using var serialPort = new SerialPort(config.SerialPortName, (int)config.BaudRate, config.Parity, (int)config.DataBits, config.StopBits)
                {
                    DtrEnable = true,
                    RtsEnable = true,
                    Encoding = Encoding.UTF8
                };

                var syncRoot = new object();
                var buffer = new StringBuilder();
                var completionSource = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

                using var inactivityTimer = new System.Timers.Timer(120)
                {
                    AutoReset = false,
                    Enabled = false
                };

                inactivityTimer.Elapsed += (_, _) =>
                {
                    lock (syncRoot)
                    {
                        if (buffer.Length == 0)
                        {
                            return;
                        }

                        completionSource.TrySetResult(ConsumeAll(buffer));
                    }
                };

                SerialDataReceivedEventHandler? dataReceivedHandler = null;
                dataReceivedHandler = (_, _) =>
                {
                    try
                    {
                        var chunk = serialPort.ReadExisting();
                        if (string.IsNullOrEmpty(chunk))
                        {
                            return;
                        }

                        lock (syncRoot)
                        {
                            buffer.Append(chunk);
                            if (QrCodeRawDataRuleHelper.TryConsume(buffer, config, out var line))
                            {
                                completionSource.TrySetResult(line);
                                return;
                            }

                            inactivityTimer.Stop();
                            inactivityTimer.Start();
                        }
                    }
                    catch (Exception ex)
                    {
                        completionSource.TrySetException(ex);
                    }
                };

                serialPort.DataReceived += dataReceivedHandler;

                using var registration = cancellationToken.Register(() => completionSource.TrySetCanceled(cancellationToken));
                try
                {
                    serialPort.Open();
                    var rawData = await completionSource.Task;
                    return rawData;
                }
                finally
                {
                    serialPort.DataReceived -= dataReceivedHandler;
                    inactivityTimer.Stop();
                }
            }
            catch (PlatformNotSupportedException ex)
            {
                throw new PlatformNotSupportedException("当前目标框架下串口扫码不可用，请切换到 net8.0-windows7.0 或更高版本。", ex);
            }
        }

        private static string ConsumeAll(StringBuilder buffer)
        {
            var content = buffer.ToString();
            buffer.Clear();
            return content;
        }
    }
}
