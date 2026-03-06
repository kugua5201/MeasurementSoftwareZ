using MeasurementSoftware.Models;
using MeasurementSoftware.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MeasurementSoftware.UserControls
{
    /// <summary>
    /// ChannelSettingUserControl.xaml 的交互逻辑
    /// </summary>
    public partial class ChannelSettingUserControl : UserControl
    {
        private bool _isVertical = true;
        private bool _isDragging;
        private ChannelAnnotation? _draggingAnnotation;

        public ChannelSettingUserControl()
        {
            InitializeComponent();
            ApplyCurrentLayout();
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ChannelSettingViewModel viewModel && viewModel.SelectedChannel != null)
            {
                viewModel.EditChannelCommand.Execute(viewModel.SelectedChannel);
            }
        }

        /// <summary>
        /// 右键点击图片时，计算相对于图片实际渲染区域的比例坐标(0~1)
        /// </summary>
        private void ProductImage_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ChannelSettingViewModel viewModel && sender is Image image)
            {
                var pos = e.GetPosition(image);
                double imgW = image.ActualWidth;
                double imgH = image.ActualHeight;

                if (imgW > 0 && imgH > 0)
                {
                    viewModel.ClickX = pos.X / imgW;
                    viewModel.ClickY = pos.Y / imgH;
                }
            }
        }

        #region 标注拖动

        private void Annotation_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ChannelAnnotation annotation)
            {
                _isDragging = true;
                _draggingAnnotation = annotation;
                fe.CaptureMouse();
                e.Handled = true;
            }
        }

        private void Annotation_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging || _draggingAnnotation == null) return;

            var pos = e.GetPosition(ProductImage);
            double imgW = ProductImage.ActualWidth;
            double imgH = ProductImage.ActualHeight;

            if (imgW > 0 && imgH > 0)
            {
                _draggingAnnotation.X = Math.Clamp(pos.X / imgW, 0, 1);
                _draggingAnnotation.Y = Math.Clamp(pos.Y / imgH, 0, 1);
            }
        }

        private void Annotation_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging && sender is FrameworkElement fe)
            {
                _isDragging = false;
                _draggingAnnotation = null;
                fe.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        #endregion

        #region 布局切换

        private void ToggleLayout_Click(object sender, RoutedEventArgs e)
        {
            _isVertical = !_isVertical;
            ApplyCurrentLayout();
        }

        private void RestoreDefaultLayout_Click(object sender, RoutedEventArgs e)
        {
            _isVertical = true;
            ApplyCurrentLayout();
        }

        private void ApplyCurrentLayout()
        {
            LayoutMenuItem.Header = _isVertical ? "切换为水平布局" : "切换为垂直布局";

            MainContentGrid.ColumnDefinitions.Clear();
            MainContentGrid.RowDefinitions.Clear();

            if (_isVertical)
            {
                // 纵向：图片(上) | 分割 | 通道列表(下)
                MainContentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 150 });
                MainContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                MainContentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.2, GridUnitType.Star), MinHeight = 150 });

                Grid.SetColumn(ImagePanel, 0); Grid.SetRow(ImagePanel, 0);
                Grid.SetColumnSpan(ImagePanel, 1); Grid.SetRowSpan(ImagePanel, 1);

                Grid.SetColumn(MainSplitter, 0); Grid.SetRow(MainSplitter, 1);
                Grid.SetColumnSpan(MainSplitter, 1); Grid.SetRowSpan(MainSplitter, 1);
                MainSplitter.Width = double.NaN; MainSplitter.Height = 6;
                MainSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                MainSplitter.VerticalAlignment = VerticalAlignment.Center;
                MainSplitter.Cursor = Cursors.SizeNS;

                Grid.SetColumn(ChannelListPanel, 0); Grid.SetRow(ChannelListPanel, 2);
                Grid.SetColumnSpan(ChannelListPanel, 1); Grid.SetRowSpan(ChannelListPanel, 1);
            }
            else
            {
                // 横向（默认）：图片(左) | 分割 | 通道列表(右)
                MainContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 250 });
                MainContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                MainContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star), MinWidth = 350 });

                Grid.SetColumn(ImagePanel, 0); Grid.SetRow(ImagePanel, 0);
                Grid.SetColumnSpan(ImagePanel, 1); Grid.SetRowSpan(ImagePanel, 1);

                Grid.SetColumn(MainSplitter, 1); Grid.SetRow(MainSplitter, 0);
                Grid.SetColumnSpan(MainSplitter, 1); Grid.SetRowSpan(MainSplitter, 1);
                MainSplitter.Width = 6; MainSplitter.Height = double.NaN;
                MainSplitter.HorizontalAlignment = HorizontalAlignment.Center;
                MainSplitter.VerticalAlignment = VerticalAlignment.Stretch;
                MainSplitter.Cursor = Cursors.SizeWE;

                Grid.SetColumn(ChannelListPanel, 2); Grid.SetRow(ChannelListPanel, 0);
                Grid.SetColumnSpan(ChannelListPanel, 1); Grid.SetRowSpan(ChannelListPanel, 1);
            }
        }

        #endregion
    }
}
