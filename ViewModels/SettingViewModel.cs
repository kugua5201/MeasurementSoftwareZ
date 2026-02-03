using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.Services.Logs;

namespace MeasurementSoftware.ViewModels
{
    /// <summary>
    /// Setting page ViewModel - Demo auto registration feature
    /// </summary>
    public partial class SettingViewModel : ObservableViewModel
    {
        [ObservableProperty]
        private string title = "Setting Page";

        [ObservableProperty]
        private bool isEnabled = true;

        private readonly ILog _log;

        public SettingViewModel(ILog log)
        {
            _log = log;
            _log.Info("SettingViewModel initialized.");
        }
    }
}