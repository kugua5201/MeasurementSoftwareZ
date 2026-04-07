using MeasurementSoftware.ViewModels;
using MeasurementSoftware.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

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

        private void DataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            if (sender is not DataGrid dataGrid || dataGrid.DataContext is not DeviceSettingViewModel viewModel || e.Row.Item is not DataPoint dataPoint)
            {
                return;
            }

            if (e.Column.Header?.ToString() != "长度")
            {
                return;
            }

            var deviceType = viewModel.SelectedDevice?.DeviceType;
            var canEdit = dataPoint.DataType == MultiProtocol.Model.FieldType.String && deviceType is not (PlcDeviceType.SiemensS7_1200 or PlcDeviceType.SiemensS7_1500);

            if (!canEdit)
            {
                e.Cancel = true;
            }
        }
    }
}
