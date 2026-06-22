using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace MeasurementSoftware.Behaviors
{
    public static class DataGridSelectedItemsBehavior
    {
        public static readonly DependencyProperty SyncedSelectedItemsProperty =
            DependencyProperty.RegisterAttached(
                "SyncedSelectedItems",
                typeof(IList),
                typeof(DataGridSelectedItemsBehavior),
                new PropertyMetadata(null, OnSyncedSelectedItemsChanged));

        public static void SetSyncedSelectedItems(DependencyObject element, IList value)
        {
            element.SetValue(SyncedSelectedItemsProperty, value);
        }

        public static IList GetSyncedSelectedItems(DependencyObject element)
        {
            return (IList)element.GetValue(SyncedSelectedItemsProperty);
        }

        private static readonly DependencyProperty IsHookedProperty =
            DependencyProperty.RegisterAttached(
                "IsHooked",
                typeof(bool),
                typeof(DataGridSelectedItemsBehavior),
                new PropertyMetadata(false));

        private static void OnSyncedSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid dataGrid)
            {
                return;
            }

            var isHooked = (bool)dataGrid.GetValue(IsHookedProperty);
            if (!isHooked)
            {
                dataGrid.SelectionChanged += DataGrid_SelectionChanged;
                dataGrid.SetValue(IsHookedProperty, true);
            }

            SyncFromGridToBoundCollection(dataGrid);
        }

        private static void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid dataGrid)
            {
                SyncFromGridToBoundCollection(dataGrid);
            }
        }

        private static void SyncFromGridToBoundCollection(DataGrid dataGrid)
        {
            var boundCollection = GetSyncedSelectedItems(dataGrid);
            if (boundCollection == null)
            {
                return;
            }

            boundCollection.Clear();

            foreach (var item in dataGrid.SelectedItems)
            {
                boundCollection.Add(item);
            }
        }
    }
}