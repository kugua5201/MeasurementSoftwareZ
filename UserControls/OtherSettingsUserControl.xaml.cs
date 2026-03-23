using HandyControl.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using MeasurementSoftware.Models;
using System.Reflection;
using System.Windows.Input;

namespace MeasurementSoftware.UserControls
{
    public partial class OtherSettingsUserControl : UserControl
    {
        private readonly Dictionary<string, SolidColorBrush?> _pendingBrushes = new();

        public OtherSettingsUserControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 初始化取色器当前颜色。
        /// </summary>
        private void ColorPicker_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not HandyControl.Controls.ColorPicker colorPicker || DataContext is not ViewModels.OtherSettingsViewModel vm)
            {
                return;
            }

            var settings = vm.CurrentRecipe?.OtherSettings;
            if (settings == null)
            {
                return;
            }

            var key = colorPicker.Tag?.ToString() ?? string.Empty;
            var currentBrush = GetCurrentBrush(settings, key);
            _pendingBrushes[key] = CloneBrush(currentBrush);
            colorPicker.SelectedBrush = CloneBrush(currentBrush);
            HideDropperButton(colorPicker);
        }

        /// <summary>
        /// 取消选色时关闭弹出框并丢弃暂存颜色。
        /// </summary>
        private void ColorPicker_Canceled(object sender, EventArgs e)
        {
            if (sender is HandyControl.Controls.ColorPicker colorPicker)
            {
                ResetDropperState(colorPicker);
                ClosePicker(colorPicker.Tag?.ToString());
            }
        }

        private void ColorPicker_Confirmed(object sender, FunctionEventArgs<Color> e)
        {
            if (sender is not HandyControl.Controls.ColorPicker colorPicker || DataContext is not ViewModels.OtherSettingsViewModel vm)
            {
                return;
            }

            var key = colorPicker.Tag?.ToString() ?? string.Empty;
            _pendingBrushes[key] = CloneBrush(colorPicker.SelectedBrush as SolidColorBrush);
            if (!_pendingBrushes.TryGetValue(key, out var brush))
            {
                ClosePicker(key);
                return;
            }

            var settings = vm.CurrentRecipe?.OtherSettings;
            if (settings == null)
            {
                ClosePicker(key);
                return;
            }

            SetCurrentBrush(settings, key, brush);
            ResetDropperState(colorPicker);
            ClosePicker(key);
        }

        private static SolidColorBrush? GetCurrentBrush(RecipeOtherSettingsConfig settings, string key) => key switch
        {
            "Ok" => settings.OkBrush,
            "Ng" => settings.NgBrush,
            "Default" => settings.DefaultBrush,
            "AnnotationText" => settings.AnnotationTextBrush,
            _ => null
        };

        private static void SetCurrentBrush(RecipeOtherSettingsConfig settings, string key, SolidColorBrush? brush)
        {
            switch (key)
            {
                case "Ok":
                    settings.OkBrush = CloneBrush(brush);
                    break;
                case "Ng":
                    settings.NgBrush = CloneBrush(brush);
                    break;
                case "Default":
                    settings.DefaultBrush = CloneBrush(brush);
                    break;
                case "AnnotationText":
                    settings.AnnotationTextBrush = CloneBrush(brush);
                    break;
            }
        }

        private static SolidColorBrush? CloneBrush(SolidColorBrush? brush)
        {
            return brush == null ? null : new SolidColorBrush(brush.Color);
        }

        private void ClosePicker(string? key)
        {
            switch (key)
            {
                case "Ok":
                    OkColorToggle.IsChecked = false;
                    break;
                case "Ng":
                    NgColorToggle.IsChecked = false;
                    break;
                case "Default":
                    DefaultColorToggle.IsChecked = false;
                    break;
                case "AnnotationText":
                    AnnotationTextColorToggle.IsChecked = false;
                    break;
            }

            if (!string.IsNullOrWhiteSpace(key))
            {
                _pendingBrushes.Remove(key);
            }
        }

        private void ColorPopup_Closed(object sender, EventArgs e)
        {
            if (sender is not Popup popup || popup.Child is not HandyControl.Controls.ColorPicker colorPicker)
            {
                return;
            }
         
            ResetDropperState(colorPicker);
            _pendingBrushes.Remove(colorPicker.Tag?.ToString() ?? string.Empty);
        }

        private static void HideDropperButton(HandyControl.Controls.ColorPicker colorPicker)
        {
            var field = colorPicker.GetType().GetField("ElementButtonDropper", BindingFlags.Public | BindingFlags.Static);
            var elementName = field?.GetValue(null) as string;
            if (string.IsNullOrWhiteSpace(elementName))
            {
                return;
            }

            if (colorPicker.Template?.FindName(elementName, colorPicker) is FrameworkElement dropperButton)
            {
                dropperButton.Visibility = Visibility.Collapsed;
                dropperButton.IsHitTestVisible = false;
            }
        }

        private static void ResetDropperState(HandyControl.Controls.ColorPicker colorPicker)
        {
            var colorDropperField = colorPicker.GetType().GetField("_colorDropper", BindingFlags.NonPublic | BindingFlags.Instance);
            var colorDropper = colorDropperField?.GetValue(colorPicker);
            var updateMethod = colorDropper?.GetType().GetMethod("Update", BindingFlags.Public | BindingFlags.Instance);
           
            var toggleField = colorPicker.GetType().GetField("_toggleButtonDropper", BindingFlags.NonPublic | BindingFlags.Instance);
            if (toggleField?.GetValue(colorPicker) is ToggleButton toggleButton && toggleButton.IsChecked == true)
            {
                var toggleMethod = colorPicker.GetType().GetMethod("ToggleButtonDropper_Click", BindingFlags.NonPublic | BindingFlags.Instance);
                toggleMethod?.Invoke(colorPicker, [toggleButton, new RoutedEventArgs(ButtonBase.ClickEvent)]);
            }

            if (toggleField?.GetValue(colorPicker) is ToggleButton dropperToggle)
            {
                dropperToggle.IsChecked = false;
            }
            updateMethod?.Invoke(colorDropper, [false]);

            Mouse.OverrideCursor = null;
            if (Mouse.LeftButton == MouseButtonState.Released && Mouse.RightButton == MouseButtonState.Released)
            {
                Mouse.UpdateCursor();
            }
        }
    }
}
