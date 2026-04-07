using MeasurementSoftware.Services.Licensing;
using System.Windows;
using HcWindow = HandyControl.Controls.Window;

namespace MeasurementSoftware
{
    public partial class RegistrationWindow : HcWindow
    {
        private readonly ILicenseService _licenseService;

        public RegistrationWindow(ILicenseService licenseService)
        {
            _licenseService = licenseService;
            InitializeComponent();
            MachineCodeTextBox.Text = _licenseService.MachineCode;
        }

        private void CopyMachineCodeButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(MachineCodeTextBox.Text);
            HandyControl.Controls.MessageBox.Show("机器码已复制到剪贴板。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            if (_licenseService.TryRegister(LicenseKeyTextBox.Text, out string errorMessage))
            {
                HandyControl.Controls.MessageBox.Show("注册成功。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
                return;
            }

            HandyControl.Controls.MessageBox.Show(errorMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
