using MeasurementSoftware.Models;
using MeasurementSoftware.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace MeasurementSoftware.UserControls
{
    /// <summary>
    /// ChannelSettingUserControl.xaml 的交互逻辑
    /// </summary>
    public partial class ChannelSettingUserControl : UserControl
    {
        public ChannelSettingUserControl()
        {
            InitializeComponent();
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ChannelSettingViewModel viewModel && viewModel.SelectedChannel != null)
            {
                viewModel.EditChannelCommand.Execute(viewModel.SelectedChannel);
            }
        }
    }
}
