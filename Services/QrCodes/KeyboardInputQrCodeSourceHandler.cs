using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services.QrCodes
{
    /// <summary>
    /// 键盘扫码数据源处理器。
    /// </summary>
    public class KeyboardInputQrCodeSourceHandler : IQrCodeSourceHandler
    {
        private readonly IKeyboardQrCodeInputService _keyboardQrCodeInputService;

        public KeyboardInputQrCodeSourceHandler(IKeyboardQrCodeInputService keyboardQrCodeInputService)
        {
            _keyboardQrCodeInputService = keyboardQrCodeInputService;
        }

        public QrCodeSourceType SourceType => QrCodeSourceType.KeyboardInput;

        public async Task<string?> WaitForRawDataAsync(QrCodeConfig config, CancellationToken cancellationToken)
        {
            return await _keyboardQrCodeInputService.WaitForNextAsync(cancellationToken);
        }
    }
}
