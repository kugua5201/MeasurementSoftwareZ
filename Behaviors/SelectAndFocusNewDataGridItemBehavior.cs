using Microsoft.Xaml.Behaviors;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace MeasurementSoftware.Behaviors
{
    /// <summary>
    /// 当 DataGrid 的集合新增项时，自动滚动到新行、选中新行，并将焦点移动到首个可编辑单元格。
    /// </summary>
    public sealed class SelectAndFocusNewDataGridItemBehavior : Behavior<DataGrid>
    {
        private INotifyCollectionChanged? _collection;
        private readonly EventHandler _itemsSourceChangedHandler;

        public SelectAndFocusNewDataGridItemBehavior()
        {
            _itemsSourceChangedHandler = (_, _) => ReattachCollectionChanged();
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty, typeof(DataGrid))?.AddValueChanged(AssociatedObject, _itemsSourceChangedHandler);
            ReattachCollectionChanged();
        }

        protected override void OnDetaching()
        {
            DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty, typeof(DataGrid))?.RemoveValueChanged(AssociatedObject, _itemsSourceChangedHandler);
            DetachCollectionChanged();
            base.OnDetaching();
        }

        private void ReattachCollectionChanged()
        {
            DetachCollectionChanged();
            _collection = AssociatedObject.ItemsSource as INotifyCollectionChanged;
            if (_collection != null)
            {
                _collection.CollectionChanged += Collection_CollectionChanged;
            }
        }

        private void DetachCollectionChanged()
        {
            if (_collection != null)
            {
                _collection.CollectionChanged -= Collection_CollectionChanged;
                _collection = null;
            }
        }

        private void Collection_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems == null || e.NewItems.Count == 0)
            {
                return;
            }

            var newItem = e.NewItems[e.NewItems.Count - 1];
            AssociatedObject.Dispatcher.BeginInvoke(() => FocusNewItem(newItem), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void FocusNewItem(object newItem)
        {
            if (AssociatedObject.Columns.Count == 0)
            {
                return;
            }

            var firstEditableColumn = AssociatedObject.Columns.FirstOrDefault(column => !column.IsReadOnly) ?? AssociatedObject.Columns[0];
            AssociatedObject.UpdateLayout();
            AssociatedObject.ScrollIntoView(newItem, firstEditableColumn);
            AssociatedObject.SelectedItem = newItem;
            AssociatedObject.CurrentCell = new DataGridCellInfo(newItem, firstEditableColumn);
            AssociatedObject.Focus();
            AssociatedObject.BeginEdit();

            if (AssociatedObject.ItemContainerGenerator.ContainerFromItem(newItem) is not DataGridRow row)
            {
                AssociatedObject.UpdateLayout();
                row = AssociatedObject.ItemContainerGenerator.ContainerFromItem(newItem) as DataGridRow;
            }

            if (row == null)
            {
                return;
            }

            var cell = GetCell(row, firstEditableColumn.DisplayIndex);
            if (cell == null)
            {
                return;
            }

            cell.Focus();
            if (FindVisualChild<FrameworkElement>(cell) is FrameworkElement element)
            {
                element.Focus();
                Keyboard.Focus(element);
            }
            else
            {
                Keyboard.Focus(cell);
            }
        }

        private static DataGridCell? GetCell(DataGridRow row, int columnIndex)
        {
            var presenter = FindVisualChild<DataGridCellsPresenter>(row);
            if (presenter == null)
            {
                row.ApplyTemplate();
                presenter = FindVisualChild<DataGridCellsPresenter>(row);
            }

            return presenter?.ItemContainerGenerator.ContainerFromIndex(columnIndex) as DataGridCell;
        }

        private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
        {
            if (parent == null)
            {
                return null;
            }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T target)
                {
                    return target;
                }

                var descendant = FindVisualChild<T>(child);
                if (descendant != null)
                {
                    return descendant;
                }
            }

            return null;
        }
    }
}
