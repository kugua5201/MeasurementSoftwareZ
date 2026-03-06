using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MeasurementSoftware.UserControls
{
    public partial class HomeUserControl : UserControl
    {
        private bool _isAlternateLayout;
        private bool _isGuidePanelVisible = true;

        public HomeUserControl()
        {
            InitializeComponent();
        }

        #region 布局控制

        /// <summary>
        /// 切换布局（默认 ↔ 备选）
        /// </summary>
        private void ToggleLayout_Click(object sender, RoutedEventArgs e)
        {
            _isAlternateLayout = !_isAlternateLayout;
        
            ApplyCurrentLayout();
        }

        /// <summary>
        /// 恢复默认布局（软件启动时的布局）
        /// </summary>
        private void RestoreDefaultLayout_Click(object sender, RoutedEventArgs e)
        {
            _isAlternateLayout = false;
            _isGuidePanelVisible = true;
            ApplyCurrentLayout();
        }

        /// <summary>
        /// 隐藏/显示导向区
        /// </summary>
        private void ToggleGuidePanel_Click(object sender, RoutedEventArgs e)
        {
            _isGuidePanelVisible = !_isGuidePanelVisible;
           
            ApplyCurrentLayout();
        }

        /// <summary>
        /// 根据当前状态统一应用布局
        /// </summary>
        private void ApplyCurrentLayout()
        {
            LayoutMenuItem.Header = _isAlternateLayout ? "切换为水平布局" : "切换为垂直布局";
            showNg.Header = _isGuidePanelVisible == true ? "隐藏导向区" : "显示导向区";
            if (_isAlternateLayout)
                ApplyAlternateLayout();
            else
                ApplyDefaultLayout();
        }

        /// <summary>
        /// 默认布局：[图片 + 导向区] 上方 | [通道表格] 下方全宽
        /// 隐藏导向区时图片铺满整行
        /// </summary>
        private void ApplyDefaultLayout()
        {
            MainContentGrid.ColumnDefinitions.Clear();
            MainContentGrid.RowDefinitions.Clear();

            if (_isGuidePanelVisible)
            {
                // 三列：图片 | 分割条 | 导向区
                MainContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 200 });
                MainContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                MainContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220), MinWidth = 180 });
            }
            else
            {
                // 一列：图片铺满
                MainContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 200 });
            }

            // 三行：上方内容 | 水平分割条 | 下方表格
            MainContentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 120 });
            MainContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            MainContentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.6, GridUnitType.Star), MinHeight = 120 });

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
        /// 备选布局：[图片] 左列全高 | [导向区 + 通道表格] 右列上下排列
        /// 隐藏导向区时表格铺满右列
        /// </summary>
        private void ApplyAlternateLayout()
        {
            MainContentGrid.ColumnDefinitions.Clear();
            MainContentGrid.RowDefinitions.Clear();

            // 三列：图片 | 分割条 | 导向区+表格
            MainContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 200 });
            MainContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            MainContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star), MinWidth = 350 });

            if (_isGuidePanelVisible)
            {
                // 三行：导向区 | 水平分割条 | 表格
                MainContentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(220) });
                MainContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                MainContentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 120 });
            }
            else
            {
                // 一行：表格铺满
                MainContentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 120 });
            }

            int totalRows = _isGuidePanelVisible ? 3 : 1;

            // 图片占满左列全部行
            Grid.SetColumn(ImagePanel, 0); Grid.SetRow(ImagePanel, 0);
            Grid.SetColumnSpan(ImagePanel, 1); Grid.SetRowSpan(ImagePanel, totalRows);
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
                // 导向区在右列上方
                Grid.SetColumn(GuidePanel, 2); Grid.SetRow(GuidePanel, 0);
                Grid.SetColumnSpan(GuidePanel, 1); Grid.SetRowSpan(GuidePanel, 1);
                GuidePanel.Visibility = Visibility.Visible;

                // 上下分割条（横线，在右列 导向区和表格之间）
                Grid.SetColumn(VerticalSplitter, 2); Grid.SetRow(VerticalSplitter, 1);
                Grid.SetColumnSpan(VerticalSplitter, 1); Grid.SetRowSpan(VerticalSplitter, 1);
                VerticalSplitter.Width = double.NaN; VerticalSplitter.Height = 6;
                VerticalSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                VerticalSplitter.VerticalAlignment = VerticalAlignment.Center;
                VerticalSplitter.Cursor = Cursors.SizeNS;
                VerticalSplitter.Visibility = Visibility.Visible;

                // 表格在右列下方
                Grid.SetColumn(TablePanel, 2); Grid.SetRow(TablePanel, 2);
                Grid.SetColumnSpan(TablePanel, 1); Grid.SetRowSpan(TablePanel, 1);
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

        #endregion
    }
}