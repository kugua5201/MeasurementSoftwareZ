using Autofac;
using HandyControl.Tools;
using MeasurementSoftware.Services.Config;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MeasurementSoftware
{
    public partial class MainWindow : Window
    {
        private double _savedMenuWidth = 250; // 保存当前菜单宽度

        public MainWindow()
        {
            InitializeComponent();
            _savedMenuWidth = LeftColumn.Width.Value;
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            var isCollecting = ((App)Application.Current).Container.Resolve<IRecipeConfigService>()?.IsCollecting ?? false;
            if (isCollecting)
            {
                var res = HandyControl.Controls.MessageBox.Show("当前正在测量，是否要退出？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (res == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetSelectedButton(HomeButton);
        }

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
            if (sender is Button selectedButton)
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
        private void SetSelectedButton(Button selectedButton)
        {
            foreach (var btn in FindVisualChildren<Button>(GridLeft))
            {
                btn.Background = Brushes.Transparent;
                foreach (var tb in FindVisualChildren<TextBlock>(btn))
                {
                    tb.Foreground = (Brush)FindResource("PrimaryTextBrush");
                }
            }
            selectedButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E3F2FD"));
            foreach (var tb in FindVisualChildren<TextBlock>(selectedButton))
            {
                tb.Foreground = (Brush)FindResource("PrimaryBrush");
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

                child = parent;
            }
            return null;
        }

        private void TabControl_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 查找点击点是否在 TabItem Header 上
            var dep = e.OriginalSource as DependencyObject;

            while (dep != null && dep is not TabItem)
            {
                dep = VisualTreeHelper.GetParent(dep);
            }

            // 如果右键点在 TabItem 上
            if (dep is TabItem)
            {
                // 阻止 TabControl 切换 SelectedItem
                e.Handled = true;
            }
        }


    }
}
