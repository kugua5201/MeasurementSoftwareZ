using MeasurementSoftware.Models;
using System.Net.Sockets;
using System.Text;

namespace MeasurementSoftware.Services.QrCodes
{
    /// <summary>
    /// 以太网扫码数据源处理器。
    /// 支持 TCP 与 UDP 两种监听方式。
    /// </summary>
    public class EthernetQrCodeSourceHandler : IQrCodeSourceHandler
    {
        public QrCodeSourceType SourceType => QrCodeSourceType.Ethernet;

        public Task<string?> WaitForRawDataAsync(QrCodeConfig config, CancellationToken cancellationToken)
        {
            return config.EthernetProtocol == NetworkProtocol.UDP
                ? WaitForUdpAsync(config, cancellationToken)
                : WaitForTcpAsync(config, cancellationToken);
        }

        private static async Task<string?> WaitForTcpAsync(QrCodeConfig config, CancellationToken cancellationToken)
        {
            if (!QrCodeRawDataRuleHelper.TryValidate(config, out var error))
            {
                throw new InvalidOperationException(error);
            }

            using var client = new TcpClient();
            await client.ConnectAsync(config.EthernetIp, config.EthernetPort, cancellationToken);

            using var stream = client.GetStream();
            var buffer = new byte[1024];
            var content = new StringBuilder();
            var idleTimeout = TimeSpan.FromMilliseconds(300);

            while (true)
            {
                int length;
                try
                {
                    using var readTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    if (content.Length > 0)
                    {
                        readTokenSource.CancelAfter(idleTimeout);
                    }

                    length = await stream.ReadAsync(buffer, readTokenSource.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && content.Length > 0)
                {
                    return content.ToString();
                }

                if (length <= 0)
                {
                    return content.Length == 0 ? null : content.ToString();
                }

                content.Append(Encoding.UTF8.GetString(buffer, 0, length));
                if (QrCodeRawDataRuleHelper.TryConsume(content, config, out var line))
                {
                    return line;
                }
            }
        }

        private static async Task<string?> WaitForUdpAsync(QrCodeConfig config, CancellationToken cancellationToken)
        {
            if (!QrCodeRawDataRuleHelper.TryValidate(config, out var error))
            {
                throw new InvalidOperationException(error);
            }

            using var udpClient = new UdpClient(config.EthernetPort);
            var result = await udpClient.ReceiveAsync(cancellationToken);
            return Encoding.UTF8.GetString(result.Buffer);
        }
    }
}
