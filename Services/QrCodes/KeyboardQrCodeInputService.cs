using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace MeasurementSoftware.Services.QrCodes
{
    /// <summary>
    /// 键盘扫码输入缓冲服务实现。
    /// </summary>
    public class KeyboardQrCodeInputService : IKeyboardQrCodeInputService
    {
        private readonly Channel<string> _channel = Channel.CreateUnbounded<string>();

        public void Submit(string rawData)
        {
            if (string.IsNullOrWhiteSpace(rawData))
            {
                return;
            }

            _channel.Writer.TryWrite(rawData.Trim());
        }

        public void ClearPending()
        {
            while (_channel.Reader.TryRead(out _))
            {
            }
        }

        public Task<string> WaitForNextAsync(CancellationToken cancellationToken)
        {
            return _channel.Reader.ReadAsync(cancellationToken).AsTask();
        }
    }
}
