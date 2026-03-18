using MeasurementSoftware.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace MeasurementSoftware.UserControls
{
    public partial class DeviceSettingUserControl : UserControl
    {
        public DeviceSettingUserControl()
        {
            InitializeComponent();
            Loaded += DeviceSettingUserControl_Loaded;
            Unloaded += DeviceSettingUserControl_Unloaded;
            IsVisibleChanged += DeviceSettingUserControl_IsVisibleChanged;
        }

        private void DeviceSettingUserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is DeviceSettingViewModel viewModel)
            {
                viewModel.SetViewActive(IsVisible);
            }
        }

        private void DeviceSettingUserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is DeviceSettingViewModel viewModel)
            {
                viewModel.SetViewActive(false);
            }
        }

        private void DeviceSettingUserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (DataContext is DeviceSettingViewModel viewModel && e.NewValue is bool isVisible)
            {
                viewModel.SetViewActive(isVisible);
            }
        }
    }
}
