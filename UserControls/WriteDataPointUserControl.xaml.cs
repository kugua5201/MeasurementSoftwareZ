using ICSharpCode.AvalonEdit;
using MeasurementSoftware.Helpers;
using MeasurementSoftware.Models;
using MeasurementSoftware.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace MeasurementSoftware.UserControls
{
    public partial class WriteDataPointUserControl : UserControl
    {
        public WriteDataPointUserControl()
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

        /// <summary>
        /// 公式脚本编辑器加载时初始化到第一行。
        /// </summary>
        private void IndirectFormulaEditorTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not TextEditor textEditor)
            {
                return;
            }

            Dispatcher.BeginInvoke(() =>
            {
                textEditor.SyntaxHighlighting = WriteValueLabelRuleHighlightingManager.GetOrCreate();
                textEditor.ScrollToHome();
                textEditor.TextArea.Caret.Offset = 0;
            }, DispatcherPriority.Background);
        }
    }
}
