using MeasurementSoftware.Models;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MeasurementSoftware.Services.UserSetting
{
    public class UserSettingsService : IUserSettingsService
    {
        private readonly string _settingsPath;
        private UserSettings _settings;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
        };

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

        public void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    _settings = JsonSerializer.Deserialize<UserSettings>(json, _jsonOptions) ?? new UserSettings();
                }
            }
            catch
            {
                _settings = new UserSettings();
            }
        }

        public void SaveSettings()
        {
            try
            {
                _settings.LastUpdated = DateTime.Now;
                var json = JsonSerializer.Serialize(_settings, _jsonOptions);
                File.WriteAllText(_settingsPath, json);
            }
            catch
            {
                // 忽略保存失败
            }
        }
    }
}
