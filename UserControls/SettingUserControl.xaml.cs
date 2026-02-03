using System.Windows.Controls;

namespace MeasurementSoftware.UserControls
{
    /// <summary>
    /// SettingUserControl.xaml 的交互逻辑
    /// </summary>
    public partial class SettingUserControl : UserControl
    {
        public SettingUserControl()
        {
            InitializeComponent();
            // DataContext 将由容器的 OnActivating 事件自动设置
        }
    }
}