using MeasurementSoftware.Models;
using MeasurementSoftware.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MeasurementSoftware.UserControls
{
    public partial class WriteDataPointDisplayUserControl : UserControl
    {
        public WriteDataPointDisplayUserControl()
        {
            InitializeComponent();
        }

        private void TextBoxValueBorder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2
                || DataContext is not WriteDataPointViewModel viewModel
                || sender is not FrameworkElement { DataContext: WriteDataPointConfig config })
            {
                return;
            }

            viewModel.BeginInlineEditFromView(config);
            e.Handled = true;
        }

        private void ButtonValueBorder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2
                || DataContext is not WriteDataPointViewModel viewModel
                || sender is not FrameworkElement { DataContext: WriteDataPointConfig config })
            {
                return;
            }

            viewModel.BeginDisplayValueEditCommand.Execute(config);
            e.Handled = true;
        }
    }
}