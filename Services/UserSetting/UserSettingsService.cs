using MeasurementSoftware.Models;
using System.IO;
using System.Text.Json;

namespace MeasurementSoftware.Services.UserSetting
{
    public class UserSettingsService : IUserSettingsService
    {
        private readonly string _settingsPath;
        private UserSettings _settings;

        public UserSettings Settings => _settings;

        public UserSettingsService()
        {
            var appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MeasurementSoftware");
            Directory.CreateDirectory(appDataFolder);
            _settingsPath = Path.Combine(appDataFolder, "user-settings.json");
            _settings = new UserSettings();
        }

        public async Task LoadSettingsAsync()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = await File.ReadAllTextAsync(_settingsPath);
                    _settings = JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
                }
            }
            catch
            {
                _settings = new UserSettings();
            }
        }

        public async Task SaveSettingsAsync()
        {
            try
            {
                _settings.LastUpdated = DateTime.Now;
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_settingsPath, json);
            }
            catch
            {
                // 忽略保存失败
            }
        }
    }
}
