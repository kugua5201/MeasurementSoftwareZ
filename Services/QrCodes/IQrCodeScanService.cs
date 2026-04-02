using MeasurementSoftware.Models;
using System.Threading;
using System.Threading.Tasks;

namespace MeasurementSoftware.Services.QrCodes
{
    /// <summary>
    /// 二维码扫描服务接口。
    /// 负责校验扫码配置、解析原始扫码数据，并根据配置等待不同来源的扫码结果。
    /// </summary>
    public interface IQrCodeScanService
    {
        /// <summary>
        /// 校验当前扫码配置是否可用于实际扫码。
        /// </summary>
        /// <param name="config">二维码配置。</param>
        /// <param name="error">校验失败原因。</param>
        /// <returns>是否通过校验。</returns>
        bool ValidateScanConfig(QrCodeConfig config, out string error);

        /// <summary>
        /// 根据配置对一段原始扫码文本做校验并提取二维码内容。
        /// </summary>
        /// <param name="config">二维码配置。</param>
        /// <param name="rawData">原始扫码文本。</param>
        /// <param name="extractedQrCode">提取后的二维码。</param>
        /// <param name="validationMessage">校验说明。</param>
        /// <returns>是否提取成功。</returns>
        bool TryExtractQrCode(QrCodeConfig config, string rawData, out string extractedQrCode, out string validationMessage);

        /// <summary>
        /// 根据配置等待一条新的有效二维码。
        /// 该方法会自动选择匹配的数据源处理器，并持续等待直到拿到通过校验的扫码结果。
        /// </summary>
        /// <param name="config">二维码配置。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>提取后的二维码文本。</returns>
        Task<string> WaitForQrCodeAsync(QrCodeConfig config, CancellationToken cancellationToken);

        /// <summary>
        /// 按当前配置接收一条原始扫码数据，并立即返回本次接收值及校验结果。
        /// 主要用于二维码设置页的单次监听验证，不依赖开始测量流程。
        /// </summary>
        /// <param name="config">二维码配置。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>本次监听是否成功、接收值、提取结果以及校验说明。</returns>
        Task<(bool Success, string RawData, string ExtractedQrCode, string Message)> ReceiveAndValidateOnceAsync(QrCodeConfig config, CancellationToken cancellationToken);

        /// <summary>
        /// 在未启用扫码功能时，根据批次流水号配置生成本次测量编号。
        /// </summary>
        /// <param name="config">二维码/流水号配置。</param>
        /// <returns>生成后的流水号。</returns>
        string GenerateBatchNumber(QrCodeConfig config);
    }
}
