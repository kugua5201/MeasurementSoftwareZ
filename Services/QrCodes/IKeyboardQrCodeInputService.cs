using System.Threading;
using System.Threading.Tasks;

namespace MeasurementSoftware.Services.QrCodes
{
    /// <summary>
    /// 键盘扫码输入缓冲服务。
    /// 负责接收页面采集到的键盘扫码内容，并向等待扫码的流程提供顺序读取能力。
    /// </summary>
    public interface IKeyboardQrCodeInputService
    {
        /// <summary>
        /// 提交一条新的键盘扫码原始数据。
        /// </summary>
        /// <param name="rawData">扫码原始文本。</param>
        void Submit(string rawData);

        /// <summary>
        /// 清空当前尚未消费的扫码数据。
        /// 用于开始新一轮测量前丢弃历史残留输入。
        /// </summary>
        void ClearPending();

        /// <summary>
        /// 等待下一条新的键盘扫码数据。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>下一条扫码原始文本。</returns>
        Task<string> WaitForNextAsync(CancellationToken cancellationToken);
    }
}
