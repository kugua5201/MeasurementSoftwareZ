using MeasurementSoftware.Extensions;
using MeasurementSoftware.Helpers;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services.UserSetting;
using MeasurementSoftware.ViewModels;
using HandyControl.Controls;
using ICSharpCode.AvalonEdit;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using HcWindow = HandyControl.Controls.Window;

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
            RestoreLayout();
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
                textEditor.SyntaxHighlighting = FormulaScriptHighlightingManager.GetOrCreate();
                textEditor.ScrollToHome();
                textEditor.TextArea.Caret.Offset = 0;
            }, DispatcherPriority.Background);
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ChannelSettingViewModel viewModel && viewModel.SelectedChannel != null)
            {
                viewModel.EditChannelCommand.Execute(viewModel.SelectedChannel);
            }
        }

        private void OpenFormulaHelp_Click(object sender, RoutedEventArgs e)
        {
            var helpMode = (sender as FrameworkElement)?.Tag?.ToString() ?? "Formula";
            var document = FormulaHelpContentProvider.CreateDocument(helpMode);
            var window = new HcWindow
            {
                Title = helpMode == "Virtual" ? "虚拟测量函数说明" : "间接测量函数说明",
                Width = 900,
                Height = 680,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = System.Windows.Window.GetWindow(this),
                Content = new Grid
                {
                    Margin = new Thickness(16),
                    Children =
                    {
                        new Border
                        {
                            Background = System.Windows.Media.Brushes.White,
                            BorderBrush = System.Windows.Media.Brushes.LightGray,
                            BorderThickness = new Thickness(1),
                            Child = new System.Windows.Controls.ScrollViewer
                            {
                                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                                Content = new RichTextBox
                                {
                                    IsReadOnly = true,
                                    BorderThickness = new Thickness(0),
                                    Background = System.Windows.Media.Brushes.Transparent,
                                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                                    FontSize = 13,
                                    Padding = new Thickness(12),
                                    Document = document,
                                    VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
                                }
                            }
                        }
                    }
                }
            };

            window.ShowDialog();
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
                SelectAnnotation(annotation);
                _isDragging = true;
                _draggingAnnotation = annotation;
                fe.CaptureMouse();
                e.Handled = true;
            }
        }

        /// <summary>
        /// 右键选中标注，便于直接弹出菜单删除。
        /// </summary>
        private void Annotation_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ChannelAnnotation annotation)
            {
                SelectAnnotation(annotation);
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

        /// <summary>
        /// 删除当前右键选中的标注。
        /// </summary>
        private void DeleteAnnotationMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem || DataContext is not ChannelSettingViewModel viewModel)
            {
                return;
            }

            var annotation = (menuItem.Parent as ContextMenu)?.PlacementTarget is FrameworkElement placementTarget
                ? placementTarget.DataContext as ChannelAnnotation
                : null;

            if (annotation != null)
            {
                viewModel.DeleteAnnotationCommand.Execute(annotation);
            }
        }

        /// <summary>
        /// 同步选中标注到视图模型。
        /// </summary>
        private void SelectAnnotation(ChannelAnnotation annotation)
        {
            if (DataContext is ChannelSettingViewModel viewModel)
            {
                viewModel.SelectAnnotationCommand.Execute(annotation);
            }
        }

        #endregion

        #region 布局切换

        private void ToggleLayout_Click(object sender, RoutedEventArgs e)
        {
            _isVertical = !_isVertical;
            ApplyCurrentLayout();
            SaveLayout();
        }

        private void RestoreDefaultLayout_Click(object sender, RoutedEventArgs e)
        {
            _isVertical = true;
            ResetLayoutSizesToDefault();
            ApplyCurrentLayout();
            SaveLayout();
        }

        /// <summary>
        /// 恢复通道设置页面布局尺寸默认值。
        /// </summary>
        private void ResetLayoutSizesToDefault()
        {
            var settings = ContainerBuilderExtensions.GetService<IUserSettingsService>();
            if (settings == null)
            {
                return;
            }

            var layout = settings.Settings.ChannelSettingLayout;
            layout.RightColumnStarWidth = ChannelSettingLayoutSettings.DefaultRightColumnStarWidth;
            layout.BottomRowStarHeight = ChannelSettingLayoutSettings.DefaultBottomRowStarHeight;
        }

        private void ApplyCurrentLayout()
        {
            var layout = ContainerBuilderExtensions.GetService<IUserSettingsService>()?.Settings.ChannelSettingLayout;

            LayoutMenuItem.Header = _isVertical ? "切换为垂直布局" : "切换为水平布局";

            MainContentGrid.ColumnDefinitions.Clear();
            MainContentGrid.RowDefinitions.Clear();

            if (_isVertical)
            {
                double bottomStarHeight = layout?.BottomRowStarHeight ?? ChannelSettingLayoutSettings.DefaultBottomRowStarHeight;
                // 纵向：图片(上) | 分割 | 通道列表(下)
                MainContentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 150 });
                MainContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                MainContentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(bottomStarHeight, GridUnitType.Star), MinHeight = 150 });

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
                double rightStarWidth = layout?.RightColumnStarWidth ?? ChannelSettingLayoutSettings.DefaultRightColumnStarWidth;
                // 横向（默认）：图片(左) | 分割 | 通道列表(右)
                MainContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 250 });
                MainContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                MainContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(rightStarWidth, GridUnitType.Star), MinWidth = 350 });

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

        /// <summary>
        /// GridSplitter 拖动完成后保存分割位置
        /// </summary>
        private void Splitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            Dispatcher.BeginInvoke(SaveSplitterPositions, DispatcherPriority.Background);
        }

        /// <summary>
        /// 保存布局模式到 UserSettings
        /// </summary>
        private void SaveLayout()
        {
            var settings = ContainerBuilderExtensions.GetService<IUserSettingsService>();
            if (settings == null) return;

            var layout = settings.Settings.ChannelSettingLayout;
            layout.IsVertical = _isVertical;
            settings.SaveSettings();
        }

        /// <summary>
        /// 保存当前 GridSplitter 拖动后的面板尺寸
        /// </summary>
        private void SaveSplitterPositions()
        {
            var settings = ContainerBuilderExtensions.GetService<IUserSettingsService>();
            if (settings == null) return;

            var layout = settings.Settings.ChannelSettingLayout;
            var cols = MainContentGrid.ColumnDefinitions;
            var rows = MainContentGrid.RowDefinitions;

            if (_isVertical)
            {
                // 纵向布局：保存下方行高比例
                if (rows.Count >= 3 && rows[0].ActualHeight > 0)
                {
                    layout.BottomRowStarHeight = rows[2].ActualHeight / rows[0].ActualHeight;
                }
            }
            else
            {
                // 横向布局：保存右列宽度比例
                if (cols.Count >= 3 && cols[0].ActualWidth > 0)
                {
                    layout.RightColumnStarWidth = cols[2].ActualWidth / cols[0].ActualWidth;
                }
            }

            settings.SaveSettings();
        }

        /// <summary>
        /// 从 UserSettings 恢复布局
        /// </summary>
        private void RestoreLayout()
        {
            var settings = ContainerBuilderExtensions.GetService<IUserSettingsService>();
            if (settings != null)
            {
                var layout = settings.Settings.ChannelSettingLayout;
                _isVertical = layout.IsVertical;
            }

            ApplyCurrentLayout();
        }

        #endregion

       
    }
}
