using System.Windows.Controls;

namespace MeasurementSoftware.UserControls
{
    /// <summary>
    /// AboutUserControl.xaml 的交互逻辑
    /// </summary>
    public partial class AboutUserControl : UserControl
    {
        public AboutUserControl()
        {
            InitializeComponent();
            // DataContext 将由容器的 OnActivating 事件自动设置
        }
    }
}