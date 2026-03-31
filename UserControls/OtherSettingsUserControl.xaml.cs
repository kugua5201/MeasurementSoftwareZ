using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MeasurementSoftware.Models;
using Forms = System.Windows.Forms;

namespace MeasurementSoftware.UserControls
{
    public partial class OtherSettingsUserControl : UserControl
    {
        public OtherSettingsUserControl()
        {
            InitializeComponent();
        }

        private void ChooseColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || DataContext is not ViewModels.OtherSettingsViewModel vm)
            {
                return;
            }

            var settings = vm.CurrentRecipe?.OtherSettings;
            if (settings == null)
            {
                return;
            }

            var key = element.Tag?.ToString() ?? string.Empty;
            var currentBrush = GetCurrentBrush(settings, key);

            using var dialog = new Forms.ColorDialog
            {
                AllowFullOpen = true,
                FullOpen = true,
                AnyColor = true,
                Color = ToDrawingColor(currentBrush?.Color)
            };

            if (dialog.ShowDialog() != Forms.DialogResult.OK)
            {
                return;
            }

            var brush = new SolidColorBrush(Color.FromArgb(dialog.Color.A, dialog.Color.R, dialog.Color.G, dialog.Color.B));
            SetCurrentBrush(settings, key, brush);
        }

        private static SolidColorBrush? GetCurrentBrush(RecipeOtherSettingsConfig settings, string key) => key switch
        {
            "Ok" => settings.OkBrush,
            "Ng" => settings.NgBrush,
            "Default" => settings.DefaultBrush,
            "Acquiring" => settings.AcquiringBrush,
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
                case "Acquiring":
                    settings.AcquiringBrush = CloneBrush(brush);
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

        private static System.Drawing.Color ToDrawingColor(Color? color)
        {
            if (color is not Color actualColor)
            {
                return System.Drawing.Color.White;
            }

            return System.Drawing.Color.FromArgb(actualColor.A, actualColor.R, actualColor.G, actualColor.B);
        }
    }
}
