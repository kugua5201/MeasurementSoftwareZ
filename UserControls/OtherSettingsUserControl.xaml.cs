using HandyControl.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace MeasurementSoftware.UserControls
{
    public partial class OtherSettingsUserControl : UserControl
    {
        public OtherSettingsUserControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 颜色选中后关闭弹出框
        /// </summary>
        private void ColorPicker_SelectedColorChanged(object sender, FunctionEventArgs<Color> e)
        {
            OkColorToggle.IsChecked = false;
            NgColorToggle.IsChecked = false;
            DefaultColorToggle.IsChecked = false;
            AnnotationTextColorToggle.IsChecked = false;
        }

        /// <summary>
        /// 取消选色后关闭弹出框
        /// </summary>
        private void ColorPicker_Canceled(object sender, EventArgs e)
        {
            OkColorToggle.IsChecked = false;
            NgColorToggle.IsChecked = false;
            DefaultColorToggle.IsChecked = false;
            AnnotationTextColorToggle.IsChecked = false;
        }
    }
}
