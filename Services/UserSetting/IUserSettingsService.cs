using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services.UserSetting
{
    /// <summary>
    /// 一些基本的配置，像软件上次打开的配方记录
    /// </summary>
    public interface IUserSettingsService
    {
        UserSettings Settings { get; }
        void LoadSettings();
        void SaveSettings();
    }
}
