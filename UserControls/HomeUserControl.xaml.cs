using MeasurementSoftware.Extensions;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services.QrCodes;
using MeasurementSoftware.Services.UserSetting;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace MeasurementSoftware.UserControls
{
    public partial class HomeUserControl : UserControl
    {
        private bool _isAlternateLayout;
        private bool _isGuidePanelVisible = true;
        private readonly StringBuilder _keyboardScanBuffer = new();

        public HomeUserControl()
        {
            InitializeComponent();
            RestoreLayout();
        }

        #region 布局控制

        /// <summary>
        /// 切换布局（默认 ↔ 备选）
        /// </summary>
        private void ToggleLayout_Click(object sender, RoutedEventArgs e)
        {
            _isAlternateLayout = !_isAlternateLayout;

            ApplyCurrentLayout();
            SaveLayout();
        }

        /// <summary>
        /// 恢复默认布局（软件启动时的布局）
        /// </summary>
        private void RestoreDefaultLayout_Click(object sender, RoutedEventArgs e)
        {
            _isAlternateLayout = false;
            _isGuidePanelVisible = true;
            ResetLayoutSizesToDefault();
            ApplyCurrentLayout();
            SaveLayout();
        }

        /// <summary>
        /// 隐藏/显示导向区
        /// </summary>
        private void ToggleGuidePanel_Click(object sender, RoutedEventArgs e)
        {
            _isGuidePanelVisible = !_isGuidePanelVisible;

            ApplyCurrentLayout();
            SaveLayout();
        }

        /// <summary>
        /// 根据当前状态统一应用布局
        /// </summary>
        private void ApplyCurrentLayout()
        {
            LayoutMenuItem.Header = _isAlternateLayout ? "切换为水平布局" : "切换为垂直布局";
            showNg.Header = _isGuidePanelVisible == true ? "隐藏导向区" : "显示导向区";
            ResetMainPanelLayoutState();
            if (_isAlternateLayout)
                ApplyAlternateLayout();
            else
                ApplyDefaultLayout();
        }

        /// <summary>
        /// 切换布局前清理主区域位置状态，避免 GuidePanel 继承旧布局的行列设置。
        /// </summary>
        private void ResetMainPanelLayoutState()
        {
            ResetElementLayout(ImagePanel);
            ResetElementLayout(GuidePanel);
            ResetElementLayout(TablePanel);
            ResetElementLayout(HorizontalSplitter);
            ResetElementLayout(VerticalSplitter);
        }

        /// <summary>
        /// 重置元素的 Grid 位置。
        /// </summary>
        private static void ResetElementLayout(UIElement element)
        {
            Grid.SetColumn(element, 0);
            Grid.SetRow(element, 0);
            Grid.SetColumnSpan(element, 1);
            Grid.SetRowSpan(element, 1);
        }

        /// <summary>
        /// 恢复测量页布局尺寸默认值。
        /// </summary>
        private void ResetLayoutSizesToDefault()
        {
            var settings = ContainerBuilderExtensions.GetService<IUserSettingsService>();
            if (settings == null)
            {
                return;
            }

            var layout = settings.Settings.HomeLayout;
            layout.GuideColumnWidth = HomeLayoutSettings.DefaultGuideColumnWidth;
            layout.TableRowStarHeight = HomeLayoutSettings.DefaultTableRowStarHeight;
            layout.AltRightColumnStarWidth = HomeLayoutSettings.DefaultAltRightColumnStarWidth;
            layout.AltGuideRowHeight = HomeLayoutSettings.DefaultAltGuideRowHeight;
        }

        /// <summary>
        /// 默认布局：[图片 + 导向区] 上方 | [通道表格] 下方全宽
        /// 隐藏导向区时图片铺满整行
        /// </summary>
        private void ApplyDefaultLayout()
        {
            var layout = ContainerBuilderExtensions.GetService<IUserSettingsService>()?.Settings.HomeLayout;

            MainContentGrid.ColumnDefinitions.Clear();
            MainContentGrid.RowDefinitions.Clear();

            if (_isGuidePanelVisible)
            {
                double guideWidth = layout?.GuideColumnWidth ?? HomeLayoutSettings.DefaultGuideColumnWidth;
                // 三列：图片 | 分割条 | 导向区
                MainContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 200 });
                MainContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                MainContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(guideWidth), MinWidth = 180 });
            }
            else
            {
                // 一列：图片铺满
                MainContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 200 });
            }

            double tableStarHeight = layout?.TableRowStarHeight ?? HomeLayoutSettings.DefaultTableRowStarHeight;
            // 三行：上方内容 | 水平分割条 | 下方表格
            MainContentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 120 });
            MainContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            MainContentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(tableStarHeight, GridUnitType.Star), MinHeight = 120 });

            // 图片
            Grid.SetColumn(ImagePanel, 0); Grid.SetRow(ImagePanel, 0);
            Grid.SetColumnSpan(ImagePanel, 1); Grid.SetRowSpan(ImagePanel, 1);
            ImagePanel.Visibility = Visibility.Visible;

            if (_isGuidePanelVisible)
            {
                // 左右分割条（竖线）
                Grid.SetColumn(HorizontalSplitter, 1); Grid.SetRow(HorizontalSplitter, 0);
                Grid.SetColumnSpan(HorizontalSplitter, 1); Grid.SetRowSpan(HorizontalSplitter, 1);
                HorizontalSplitter.Width = 6; HorizontalSplitter.Height = double.NaN;
                HorizontalSplitter.HorizontalAlignment = HorizontalAlignment.Center;
                HorizontalSplitter.VerticalAlignment = VerticalAlignment.Stretch;
                HorizontalSplitter.Cursor = Cursors.SizeWE;
                HorizontalSplitter.Visibility = Visibility.Visible;

                // 导向区
                Grid.SetColumn(GuidePanel, 2); Grid.SetRow(GuidePanel, 0);
                Grid.SetColumnSpan(GuidePanel, 1); Grid.SetRowSpan(GuidePanel, 1);
                GuidePanel.Visibility = Visibility.Visible;
            }
            else
            {
                HorizontalSplitter.Visibility = Visibility.Collapsed;
                GuidePanel.Visibility = Visibility.Collapsed;
            }

            int totalCols = _isGuidePanelVisible ? 3 : 1;

            // 上下分割条（横线，全宽）
            Grid.SetColumn(VerticalSplitter, 0); Grid.SetRow(VerticalSplitter, 1);
            Grid.SetColumnSpan(VerticalSplitter, totalCols); Grid.SetRowSpan(VerticalSplitter, 1);
            VerticalSplitter.Width = double.NaN; VerticalSplitter.Height = 6;
            VerticalSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalSplitter.VerticalAlignment = VerticalAlignment.Center;
            VerticalSplitter.Cursor = Cursors.SizeNS;
            VerticalSplitter.Visibility = Visibility.Visible;

            // 表格（全宽）
            Grid.SetColumn(TablePanel, 0); Grid.SetRow(TablePanel, 2);
            Grid.SetColumnSpan(TablePanel, totalCols); Grid.SetRowSpan(TablePanel, 1);
            TablePanel.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 备选布局：[图片 + 导向区] 左列上下排列 | [通道表格] 右列全高
        /// 隐藏导向区时图片与表格左右排列
        /// </summary>
        private void ApplyAlternateLayout()
        {
            var layout = ContainerBuilderExtensions.GetService<IUserSettingsService>()?.Settings.HomeLayout;

            MainContentGrid.ColumnDefinitions.Clear();
            MainContentGrid.RowDefinitions.Clear();

            double rightStarWidth = layout?.AltRightColumnStarWidth ?? HomeLayoutSettings.DefaultAltRightColumnStarWidth;
            // 三列：左侧图片区/导向区 | 分割条 | 右侧通道表格
            MainContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 200 });
            MainContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            MainContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(rightStarWidth, GridUnitType.Star), MinWidth = 350 });

            if (_isGuidePanelVisible)
            {
                double guideRowHeight = layout?.AltGuideRowHeight ?? HomeLayoutSettings.DefaultAltGuideRowHeight;
                // 三行：图片 | 水平分割条 | 导向区
                MainContentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 120 });
                MainContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                MainContentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(guideRowHeight), MinHeight = 120 });
            }
            else
            {
                // 一行：图片与表格左右排列
                MainContentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 120 });
            }

            int totalRows = _isGuidePanelVisible ? 3 : 1;

            // 图片在左列上方
            Grid.SetColumn(ImagePanel, 0); Grid.SetRow(ImagePanel, 0);
            Grid.SetColumnSpan(ImagePanel, 1); Grid.SetRowSpan(ImagePanel, 1);
            ImagePanel.Visibility = Visibility.Visible;

            // 左右分割条（竖线，全高）
            Grid.SetColumn(HorizontalSplitter, 1); Grid.SetRow(HorizontalSplitter, 0);
            Grid.SetColumnSpan(HorizontalSplitter, 1); Grid.SetRowSpan(HorizontalSplitter, totalRows);
            HorizontalSplitter.Width = 6; HorizontalSplitter.Height = double.NaN;
            HorizontalSplitter.HorizontalAlignment = HorizontalAlignment.Center;
            HorizontalSplitter.VerticalAlignment = VerticalAlignment.Stretch;
            HorizontalSplitter.Cursor = Cursors.SizeWE;
            HorizontalSplitter.Visibility = Visibility.Visible;

            if (_isGuidePanelVisible)
            {
                // 导向区在左列下方
                Grid.SetColumn(GuidePanel, 0); Grid.SetRow(GuidePanel, 2);
                Grid.SetColumnSpan(GuidePanel, 1); Grid.SetRowSpan(GuidePanel, 1);
                GuidePanel.Visibility = Visibility.Visible;

                // 上下分割条（横线，在左列 图片和导向区之间）
                Grid.SetColumn(VerticalSplitter, 0); Grid.SetRow(VerticalSplitter, 1);
                Grid.SetColumnSpan(VerticalSplitter, 1); Grid.SetRowSpan(VerticalSplitter, 1);
                VerticalSplitter.Width = double.NaN; VerticalSplitter.Height = 6;
                VerticalSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                VerticalSplitter.VerticalAlignment = VerticalAlignment.Center;
                VerticalSplitter.Cursor = Cursors.SizeNS;
                VerticalSplitter.Visibility = Visibility.Visible;

                // 表格在右列整列显示
                Grid.SetColumn(TablePanel, 2); Grid.SetRow(TablePanel, 0);
                Grid.SetColumnSpan(TablePanel, 1); Grid.SetRowSpan(TablePanel, totalRows);
            }
            else
            {
                GuidePanel.Visibility = Visibility.Collapsed;
                VerticalSplitter.Visibility = Visibility.Collapsed;

                // 表格铺满右列全部行
                Grid.SetColumn(TablePanel, 2); Grid.SetRow(TablePanel, 0);
                Grid.SetColumnSpan(TablePanel, 1); Grid.SetRowSpan(TablePanel, 1);
            }

            TablePanel.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// GridSplitter 拖动完成后保存分割位置
        /// </summary>
        private void Splitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            Dispatcher.BeginInvoke(SaveSplitterPositions, DispatcherPriority.Background);
        }

        /// <summary>
        /// 保存布局模式（切换布局/显隐导向区时调用）
        /// </summary>
        private void SaveLayout()
        {
            var settings = ContainerBuilderExtensions.GetService<IUserSettingsService>();
            if (settings == null) return;

            var layout = settings.Settings.HomeLayout;
            layout.IsAlternateLayout = _isAlternateLayout;
            layout.IsGuidePanelVisible = _isGuidePanelVisible;
            settings.SaveSettings();
        }

        /// <summary>
        /// 保存当前 GridSplitter 拖动后的面板尺寸
        /// </summary>
        private void SaveSplitterPositions()
        {
            var settings = ContainerBuilderExtensions.GetService<IUserSettingsService>();
            if (settings == null) return;

            var layout = settings.Settings.HomeLayout;
            var cols = MainContentGrid.ColumnDefinitions;
            var rows = MainContentGrid.RowDefinitions;

            if (!_isAlternateLayout)
            {
                // 默认布局：导向区列宽（绝对值）、表格行高比例（Star比）
                if (_isGuidePanelVisible && cols.Count >= 3 && cols[2].ActualWidth > 0)
                {
                    layout.GuideColumnWidth = cols[2].ActualWidth;
                }
                if (rows.Count >= 3 && rows[0].ActualHeight > 0)
                {
                    layout.TableRowStarHeight = rows[2].ActualHeight / rows[0].ActualHeight;
                }
            }
            else
            {
                // 备选布局：右列宽度比例（Star比）、导向区行高（绝对值）
                if (cols.Count >= 3 && cols[0].ActualWidth > 0)
                {
                    layout.AltRightColumnStarWidth = cols[2].ActualWidth / cols[0].ActualWidth;
                }
                if (_isGuidePanelVisible && rows.Count >= 3 && rows[0].ActualHeight > 0)
                {
                    layout.AltGuideRowHeight = rows[0].ActualHeight;
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
                var layout = settings.Settings.HomeLayout;
                _isAlternateLayout = layout.IsAlternateLayout;
                _isGuidePanelVisible = layout.IsGuidePanelVisible;
            }

            ApplyCurrentLayout();
        }

        /// <summary>
        /// 页面加载后主动获取焦点，便于键盘扫码枪直接把输入送到首页。
        /// </summary>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => Focus()));
        }

        /// <summary>
        /// 收集扫码枪以键盘方式发送的字符数据。
        /// </summary>
        private void UserControl_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text))
            {
                return;
            }

            _keyboardScanBuffer.Append(e.Text);
        }

        /// <summary>
        /// 以回车或 Tab 作为一次扫码输入结束标记，并提交给扫码输入缓冲服务。
        /// </summary>
        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Back)
            {
                if (_keyboardScanBuffer.Length > 0)
                {
                    _keyboardScanBuffer.Remove(_keyboardScanBuffer.Length - 1, 1);
                }

                return;
            }

            if (e.Key != Key.Enter && e.Key != Key.Return && e.Key != Key.Tab)
            {
                return;
            }

            var rawData = _keyboardScanBuffer.ToString().Trim();
            _keyboardScanBuffer.Clear();
            if (string.IsNullOrWhiteSpace(rawData))
            {
                return;
            }

            ContainerBuilderExtensions.GetService<IKeyboardQrCodeInputService>()?.Submit(rawData);
            e.Handled = true;
        }

        #endregion
    }
}