using MeasurementSoftware.Models;
using MeasurementSoftware.ViewModels;
using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;

namespace MeasurementSoftware.Behaviors
{
    /// <summary>
    /// 右键页签头时，先将当前页签设置为选中页签，确保菜单命令和目标页列表正确。
    /// </summary>
    public sealed class SelectWriteDataPointPageOnContextMenuBehavior : Behavior<FrameworkElement>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.ContextMenuOpening += AssociatedObject_ContextMenuOpening;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.ContextMenuOpening -= AssociatedObject_ContextMenuOpening;
            base.OnDetaching();
        }

        private void AssociatedObject_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (AssociatedObject.DataContext is not WriteDataPointPageConfig page)
            {
                return;
            }

            if (FindAncestor<UserControl>(AssociatedObject)?.DataContext is not WriteDataPointViewModel viewModel)
            {
                return;
            }

            viewModel.SelectedEditPage = page;
        }

        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            DependencyObject? parent = current;
            while (parent != null)
            {
                if (parent is T target)
                {
                    return target;
                }

                parent = LogicalTreeHelper.GetParent(parent) ?? System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }

            return null;
        }
    }
}
