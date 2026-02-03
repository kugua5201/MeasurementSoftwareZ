using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services
{
    public interface IUserSettingsService
    {
        UserSettings Settings { get; }
        Task LoadSettingsAsync();
        Task SaveSettingsAsync();
    }
}
