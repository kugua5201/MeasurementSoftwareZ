using MeasurementSoftware.Models;
using System.Threading;
using System.Threading.Tasks;

namespace MeasurementSoftware.Services.QrCodes
{
    /// <summary>
    /// 二维码数据源处理器接口。
    /// 不同扫码来源（键盘、串口、以太网、PLC）分别由各自实现负责读取原始数据。
    /// </summary>
    public interface IQrCodeSourceHandler
    {
        /// <summary>
        /// 当前处理器支持的数据源类型。
        /// </summary>
        QrCodeSourceType SourceType { get; }

        /// <summary>
        /// 按当前扫码配置等待一条新的原始扫码数据。
        /// </summary>
        /// <param name="config">二维码配置。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>原始扫码文本。</returns>
        Task<string?> WaitForRawDataAsync(QrCodeConfig config, CancellationToken cancellationToken);
    }
}
