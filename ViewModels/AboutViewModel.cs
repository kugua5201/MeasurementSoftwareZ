using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.Services.Logs;

namespace MeasurementSoftware.ViewModels
{
    public partial class AboutViewModel : ObservableViewModel
    {
        [ObservableProperty]
        private string title = "About Page";

        [ObservableProperty]
        private string version = "1.0.0";

        [ObservableProperty]
        private string description = "This is a measurement software application built with WPF and MVVM pattern.";


        private readonly ILog _log;

        public AboutViewModel(ILog log)
        {
            _log = log;
            _log.Info("AboutViewModel initialized.");
        }
    }
}