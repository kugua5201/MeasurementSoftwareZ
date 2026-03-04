using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services.Config
{
    /// <summary>
    /// 二维码配置服务接口
    /// </summary>
    public interface IQrCodeConfigService
    {
        QrCodeConfig QrCodeConfig { get; set; }
        Task<bool> SaveQrCodeConfigAsync();
    }
}
