using ICSharpCode.AvalonEdit;
using MeasurementSoftware.Helpers;
using MeasurementSoftware.Models;
using MeasurementSoftware.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MeasurementSoftware.UserControls
{
    public partial class WriteDataPointUserControl : UserControl
    {
        private Point _dragStartPoint;
        private WriteDataPointConfig? _draggedItem;

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

        private void DataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);

            if (e.OriginalSource is not DependencyObject source)
            {
                _draggedItem = null;
                return;
            }

            var row = FindAncestor<DataGridRow>(source);
            _draggedItem = row?.Item as WriteDataPointConfig;
        }

        private void DataGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _draggedItem is null)
            {
                return;
            }

            Point currentPosition = e.GetPosition(null);
            Vector diff = _dragStartPoint - currentPosition;

            if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            if (sender is not DataGrid dataGrid)
            {
                return;
            }

            DragDrop.DoDragDrop(dataGrid, _draggedItem, DragDropEffects.Move);
            e.Handled = true;
          
        }

        private void DataGrid_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(typeof(WriteDataPointConfig))
                ? DragDropEffects.Move
                : DragDropEffects.None;

            e.Handled = true;
        }

        private void DataGrid_Drop(object sender, DragEventArgs e)
        {
            if (DataContext is not WriteDataPointViewModel viewModel)
            {
                return;
            }

            if (!e.Data.GetDataPresent(typeof(WriteDataPointConfig)))
            {
                return;
            }

            if (e.Data.GetData(typeof(WriteDataPointConfig)) is not WriteDataPointConfig sourceItem)
            {
                return;
            }

            WriteDataPointConfig? targetItem = null;

            if (e.OriginalSource is DependencyObject source)
            {
                var targetRow = FindAncestor<DataGridRow>(source);
                targetItem = targetRow?.Item as WriteDataPointConfig;
            }

            viewModel.MoveWriteDataPoint(sourceItem, targetItem);
            _draggedItem = null;
            e.Handled = true;
        }

        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match)
                {
                    return match;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }
    }
}