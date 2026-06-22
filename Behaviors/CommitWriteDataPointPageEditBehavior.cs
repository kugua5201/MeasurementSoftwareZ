using MeasurementSoftware.Models;
using MeasurementSoftware.ViewModels;
using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace MeasurementSoftware.Behaviors
{
    /// <summary>
    /// 在页签名称编辑时，支持回车或失焦提交名称。
    /// </summary>
    public sealed class CommitWriteDataPointPageEditBehavior : Behavior<TextBox>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.KeyDown += AssociatedObject_KeyDown;
            AssociatedObject.LostFocus += AssociatedObject_LostFocus;
            AssociatedObject.IsVisibleChanged += AssociatedObject_IsVisibleChanged;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.KeyDown -= AssociatedObject_KeyDown;
            AssociatedObject.LostFocus -= AssociatedObject_LostFocus;
            AssociatedObject.IsVisibleChanged -= AssociatedObject_IsVisibleChanged;
            base.OnDetaching();
        }

        private void AssociatedObject_KeyDown(object sender, KeyEventArgs e)
        {
            if (TryGetContext(out var viewModel, out var page) == false)
            {
                return;
            }

            if (e.Key == Key.Enter)
            {
                viewModel.CommitWriteDataPointPageEdit(page);
                Keyboard.ClearFocus();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                viewModel.CancelWriteDataPointPageEdit(page);
                Keyboard.ClearFocus();
                e.Handled = true;
                return;
            }
        }

        private void AssociatedObject_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitEdit();
        }

        private void AssociatedObject_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!AssociatedObject.IsVisible)
            {
                return;
            }

            AssociatedObject.Dispatcher.BeginInvoke(new Action(() =>
            {
                Keyboard.Focus(AssociatedObject);
                AssociatedObject.SelectAll();
            }), DispatcherPriority.Input);
        }

        private void CommitEdit()
        {
            if (!TryGetContext(out var viewModel, out var page))
            {
                return;
            }

            viewModel.CommitWriteDataPointPageEdit(page);
        }

        private bool TryGetContext(out WriteDataPointViewModel viewModel, out WriteDataPointPageConfig page)
        {
            viewModel = null!;
            page = null!;

            if (AssociatedObject.DataContext is not WriteDataPointPageConfig currentPage)
            {
                return false;
            }

            if (FindAncestor<UserControl>(AssociatedObject)?.DataContext is not WriteDataPointViewModel currentViewModel)
            {
                return false;
            }

            page = currentPage;
            viewModel = currentViewModel;
            return true;
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
