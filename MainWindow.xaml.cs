using Autofac;
using HandyControl.Controls;
using HandyControl.Data;
using HandyControl.Tools;
using MeasurementSoftware.Extensions;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Config;
using MeasurementSoftware.Services.UserSetting;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DrawingIcon = System.Drawing.Icon;
using Forms = System.Windows.Forms;
using HcWindow = HandyControl.Controls.Window;

namespace MeasurementSoftware
{
    public partial class MainWindow : HcWindow
    {
        private double _savedMenuWidth = 250; // 保存当前菜单宽度
        private readonly Forms.NotifyIcon _notifyIcon = new();
        private bool _isExitRequested;
        private bool _isHiddenToBackground;
        private DrawingIcon? _trayIcon;

        public MainWindow()
        {
            InitializeComponent();
            _savedMenuWidth = LeftColumn.Width.Value;

            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
            //this.StateChanged += MainWindow_StateChanged;
            this.Closed += MainWindow_Closed;
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (!_isExitRequested)
            {
                e.Cancel = true;
                HideToBackground();
                return;
            }

            var isCollecting = ((App)System.Windows.Application.Current).Container.Resolve<IRecipeConfigService>()?.IsCollecting ?? false;
            if (isCollecting)
            {
                var res = HandyControl.Controls.MessageBox.Show("当前正在测量，是否要退出？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (res == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    _isExitRequested = false;
                    return;
                }
            }

            SaveWindowLayout();
            if (DataContext is ViewModels.MainWindowViewModel viewModel)
            {
                viewModel.SaveNavigationLayout();
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeNotifyIcon();
            RestoreWindowLayout();
            RestoreSelectedMenuButton();
        }

        //private void MainWindow_StateChanged(object? sender, EventArgs e)
        //{
        //    if (_isExitRequested)
        //    {
        //        return;
        //    }

        //    if (WindowState == WindowState.Minimized)
        //    {
        //        HideToBackground();
        //    }
        //}

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _trayIcon?.Dispose();
        }

        private void InitializeNotifyIcon()
        {
            if (_notifyIcon.ContextMenuStrip != null)
            {
                return;
            }

            _trayIcon = LoadTrayIcon();
            _notifyIcon.Icon = _trayIcon ?? System.Drawing.SystemIcons.Application;
            _notifyIcon.Text = "测量软件";
            _notifyIcon.Visible = true;
            _notifyIcon.DoubleClick += NotifyIcon_DoubleClick;

            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("显示主窗口", null, (_, _) => RestoreFromBackground());
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("退出", null, (_, _) => ExitApplication());
            _notifyIcon.ContextMenuStrip = menu;
        }

        private DrawingIcon? LoadTrayIcon()
        {
            DrawingIcon? icon = Properties.Resources.ico is byte[] icoBytes ? new DrawingIcon(new MemoryStream(icoBytes)) : null;
            return icon;
        }

        private void HideToBackground()
        {
            if (_isHiddenToBackground)
            {
                return;
            }

            _isHiddenToBackground = true;
            _notifyIcon.Visible = true;
            ShowInTaskbar = false;
            Hide();
            ShowTrayNotification();
        }

        private void ShowTrayNotification()
        {
            try
            {
                _notifyIcon.ShowBalloonTip(3000, "测量软件", "应用已最小化到系统托盘，点击托盘图标恢复。", Forms.ToolTipIcon.Info);
            }
            catch
            {
            }
        }

        private void RestoreFromBackground()
        {
            if (!_isHiddenToBackground)
            {
                return;
            }

            _isHiddenToBackground = false;
            ShowInTaskbar = true;
            Show();
            WindowState = WindowState.Normal;
            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
        }

        private void NotifyIcon_DoubleClick(object? sender, EventArgs e)
        {
            RestoreFromBackground();
        }

        private void ExitApplication()
        {
            _isExitRequested = true;

            if (_isHiddenToBackground)
            {
                _isHiddenToBackground = false;
                Show();
                ShowInTaskbar = true;
                WindowState = WindowState.Normal;
            }

            Close();
        }

        #region 窗口布局保存/恢复

        private void SaveWindowLayout()
        {
            var settings = ContainerBuilderExtensions.GetService<IUserSettingsService>();
            if (settings == null) return;

            var layout = settings.Settings.WindowLayout;

            // 保存窗口状态
            layout.IsMaximized = WindowState == WindowState.Maximized;
            if (WindowState == WindowState.Maximized)
            {
                // 最大化时用 RestoreBounds 保存正常状态的位置和大小
                var bounds = RestoreBounds;
                layout.Left = bounds.Left;
                layout.Top = bounds.Top;
                layout.Width = bounds.Width;
                layout.Height = bounds.Height;
            }
            else
            {
                layout.Left = Left;
                layout.Top = Top;
                layout.Width = Width;
                layout.Height = Height;
            }

            // 保存菜单列宽（使用当前值或保存的值）
            layout.MenuColumnWidth = LeftColumn.Width.Value > 0 ? LeftColumn.Width.Value : _savedMenuWidth;
        }

        private void RestoreWindowLayout()
        {
            var settings = ContainerBuilderExtensions.GetService<IUserSettingsService>();
            if (settings == null) return;

            var layout = settings.Settings.WindowLayout;

            // 恢复窗口大小
            if (layout.Width > 0) Width = layout.Width;
            if (layout.Height > 0) Height = layout.Height;

            // 恢复窗口位置（检查是否在屏幕范围内）
            if (!double.IsNaN(layout.Left) && !double.IsNaN(layout.Top))
            {
                var virtualLeft = SystemParameters.VirtualScreenLeft;
                var virtualTop = SystemParameters.VirtualScreenTop;
                var virtualWidth = SystemParameters.VirtualScreenWidth;
                var virtualHeight = SystemParameters.VirtualScreenHeight;

                if (layout.Left >= virtualLeft && layout.Left < virtualLeft + virtualWidth &&
                    layout.Top >= virtualTop && layout.Top < virtualTop + virtualHeight)
                {
                    Left = layout.Left;
                    Top = layout.Top;
                    WindowStartupLocation = WindowStartupLocation.Manual;
                }
            }

            // 恢复最大化状态
            if (layout.IsMaximized)
            {
                WindowState = WindowState.Maximized;
            }

            // 恢复菜单列宽
            if (layout.MenuColumnWidth > 0)
            {
                LeftColumn.Width = new GridLength(layout.MenuColumnWidth);
                _savedMenuWidth = layout.MenuColumnWidth;
            }
        }

        private void RestoreSelectedMenuButton()
        {
            if (DataContext is not ViewModels.MainWindowViewModel viewModel)
            {
                SetSelectedButton(HomeButton);
                return;
            }

            var button = GetMenuButtonByHeader(viewModel.SelectedTab?.Header) ?? HomeButton;
            SetSelectedButton(button);

            var parentExpander = FindParentExpander(button);
            if (parentExpander != null)
            {
                parentExpander.IsExpanded = true;
            }
        }

        #endregion

        private void GridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            // 保存当前菜单宽度
            _savedMenuWidth = LeftColumn.Width.Value;
        }

        private void OnLeftMainContentShiftOut(object sender, RoutedEventArgs e)
        {
            ButtonShiftOut.Visibility = Visibility.Collapsed;

            double targetValue = -LeftColumn.ActualWidth;

            // 保存当前宽度（如果还没保存的话）
            if (_savedMenuWidth == 250 || Math.Abs(_savedMenuWidth - LeftColumn.Width.Value) > 1)
            {
                _savedMenuWidth = LeftColumn.Width.Value;
            }

            var animation = AnimationHelper.CreateAnimation(targetValue, milliseconds: 300);
            animation.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            animation.FillBehavior = FillBehavior.Stop;
            animation.Completed += OnAnimationCompleted;
            LeftTransform.BeginAnimation(TranslateTransform.XProperty, animation);

            void OnAnimationCompleted(object? _, EventArgs args)
            {
                animation.Completed -= OnAnimationCompleted;
                LeftTransform.SetCurrentValue(TranslateTransform.XProperty, targetValue);

                // 调整布局 - 注意这里要适配新的列结构
                Grid.SetColumn(MainContent, 0);
                Grid.SetColumnSpan(MainContent, 3);

                LeftColumn.Width = new GridLength(0);
                ButtonShiftIn.Visibility = Visibility.Visible;
            }
        }

        private void OnLeftMainContentShiftIn(object sender, RoutedEventArgs e)
        {
            ButtonShiftIn.Visibility = Visibility.Collapsed;

            // 恢复到保存的宽度
            var restoredWidth = new GridLength(_savedMenuWidth);
            LeftColumn.Width = restoredWidth;

            Grid.SetColumn(MainContent, 2);
            Grid.SetColumnSpan(MainContent, 1);

            double targetValue = 0;

            var animation = AnimationHelper.CreateAnimation(targetValue, milliseconds: 300);
            animation.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            animation.FillBehavior = FillBehavior.Stop;
            animation.Completed += OnAnimationCompleted;
            LeftTransform.BeginAnimation(TranslateTransform.XProperty, animation);

            void OnAnimationCompleted(object? _, EventArgs args)
            {
                animation.Completed -= OnAnimationCompleted;
                LeftTransform.SetCurrentValue(TranslateTransform.XProperty, targetValue);
                ButtonShiftOut.Visibility = Visibility.Visible;
            }
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button selectedButton)
            {
                SetSelectedButton(selectedButton);

                var parentExpander = FindParentExpander(selectedButton);
                if (parentExpander == null)
                {
                    //折叠所有 Expander
                    foreach (var expander in FindVisualChildren<Expander>(GridLeft))
                    {
                        expander.IsExpanded = false;
                    }
                }
            }

        }
        private void SetSelectedButton(System.Windows.Controls.Button selectedButton)
        {
            foreach (var btn in FindVisualChildren<System.Windows.Controls.Button>(GridLeft))
            {
                btn.Background = System.Windows.Media.Brushes.Transparent;
                foreach (var tb in FindVisualChildren<TextBlock>(btn))
                {
                    tb.Foreground = (System.Windows.Media.Brush)FindResource("PrimaryTextBrush");
                }
            }
            selectedButton.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E3F2FD"));
            foreach (var tb in FindVisualChildren<TextBlock>(selectedButton))
            {
                tb.Foreground = (System.Windows.Media.Brush)FindResource("PrimaryBrush");
            }
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            if (sender is Expander currentExpander)
            {
                // 获取父容器中的所有 Expander
                if (currentExpander.Parent is StackPanel parent)
                {
                    foreach (var child in parent.Children)
                    {
                        if (child is Expander expander && expander != currentExpander)
                        {
                            expander.IsExpanded = false;
                        }
                    }
                }
            }


        }



        private T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child != null && child is T t)
                {
                    return t;
                }

                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t)
                    yield return t;
                foreach (var childOfChild in FindVisualChildren<T>(child))
                    yield return childOfChild;
            }
        }

        private Expander? FindParentExpander(DependencyObject child)
        {
            while (child != null)
            {
                if (child is Expander expander)
                    return expander;

                // 先查视觉树
                var parent = VisualTreeHelper.GetParent(child);

                // 如果视觉树没有，再查逻辑树
                if (parent == null)
                    parent = LogicalTreeHelper.GetParent(child);

                // 如果还没有，尝试用 FrameworkElement 的 Parent 属性
                if (parent == null && child is FrameworkElement fe)
                    parent = fe.Parent;

                child = parent!;
            }
            return null;
        }

        private void TabControl_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 查找点击点是否在 TabItem Header 上
            var dep = e.OriginalSource as DependencyObject;

            while (dep != null && dep is not HandyControl.Controls.TabItem)
            {
                dep = VisualTreeHelper.GetParent(dep);
            }

            // 如果右键点在 TabItem 上
            if (dep is HandyControl.Controls.TabItem)
            {
                // 阻止 TabControl 切换 SelectedItem
                e.Handled = true;
            }
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not HandyControl.Controls.TabControl tc || tc.SelectedItem is not TabItemModel tab)
                return;

            // 根据 tab Header 找到对应的菜单按钮
            var button = GetMenuButtonByHeader(tab.Header);
            if (button != null)
            {
                SetSelectedButton(button);

                // 如果是子菜单按钮，展开 Expander
                var parentExpander = FindParentExpander(button);
                if (parentExpander != null)
                    parentExpander.IsExpanded = true;
                else
                {
                    // 非子菜单，折叠所有 Expander
                    foreach (var expander in FindVisualChildren<Expander>(GridLeft))
                        expander.IsExpanded = false;
                }
            }
        }

        private System.Windows.Controls.Button? GetMenuButtonByHeader(string? header)
        {
            return header switch
            {
                "测量" => HomeButton,
                "配方管理" => RecipeManagementButton,
                "校准" => CalibrationButton,
                "数据管理" => DataManagementButton,
                "SPC分析" => SpcButton,
                "通道设置" => ChannelSettingButton,
                "设备管理" => CommunicationButton,
                "二维码配置" => QrCodeSettingButton,
                "MES配置" => MesSettingButton,
                "其他设置" => OtherSettingsButton,
                "日志" => LogViewerButton,
                _ => null
            };
        }


    }
}
