using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Xaml.Behaviors;

namespace MeasurementSoftware.Behaviors
{
    public sealed class DataGridSelectAndScrollBehavior : Behavior<DataGrid>
    {
        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(
                nameof(SelectedItem),
                typeof(object),
                typeof(DataGridSelectAndScrollBehavior),
                new PropertyMetadata(null, OnSelectedItemChanged));

        public object? SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGridSelectAndScrollBehavior behavior)
            {
                return;
            }

            var grid = behavior.AssociatedObject;
            if (grid == null || e.NewValue == null)
            {
                return;
            }

            grid.Dispatcher.BeginInvoke(new Action(() =>
            {
                grid.ScrollIntoView(e.NewValue);
                grid.SelectedItem = e.NewValue;
                grid.Focus();
                Keyboard.Focus(grid);
                grid.UpdateLayout();
            }), DispatcherPriority.Input);
        }
    }
}