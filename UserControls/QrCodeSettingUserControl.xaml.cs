using MeasurementSoftware.Extensions;
using MeasurementSoftware.Services.QrCodes;
using MeasurementSoftware.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace MeasurementSoftware.UserControls
{
    public partial class QrCodeSettingUserControl : UserControl
    {
        private QrCodeSettingViewModel? _viewModel;

        private TextBox? TestRawDataInputTextBox => FindName("TestRawDataInputBox") as TextBox;

        public QrCodeSettingUserControl()
        {
            InitializeComponent();
            DataContextChanged += QrCodeSettingUserControl_DataContextChanged;
        }

        private void QrCodeSettingUserControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            _viewModel = DataContext as QrCodeSettingViewModel;
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_viewModel == null)
            {
                return;
            }

            if (e.PropertyName == nameof(QrCodeSettingViewModel.IsListeningValidation)
                || e.PropertyName == nameof(QrCodeSettingViewModel.Config))
            {
                FocusKeyboardListenInputIfNeeded();
            }
        }

        private void FocusKeyboardListenInputIfNeeded()
        {
            if (_viewModel?.IsListeningValidation != true || _viewModel.IsKeyboardInputVisible != true)
            {
                return;
            }

            Dispatcher.BeginInvoke(() =>
            {
                TestRawDataInputTextBox?.Focus();
                if (TestRawDataInputTextBox != null)
                {
                    Keyboard.Focus(TestRawDataInputTextBox);
                    TestRawDataInputTextBox.SelectAll();
                }
            }, DispatcherPriority.Input);
        }

        private void TestRawDataInputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_viewModel?.IsListeningValidation != true || _viewModel.IsKeyboardInputVisible != true)
            {
                return;
            }

            if (e.Key != Key.Enter && e.Key != Key.Return && e.Key != Key.Tab)
            {
                return;
            }

            var rawData = TestRawDataInputTextBox?.Text.Trim() ?? string.Empty;
            TestRawDataInputTextBox?.Clear();
            if (string.IsNullOrWhiteSpace(rawData))
            {
                e.Handled = true;
                return;
            }

            ContainerBuilderExtensions.GetService<IKeyboardQrCodeInputService>()?.Submit(rawData);
            e.Handled = true;
        }
    }
}
