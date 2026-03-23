using MeasurementSoftware.Models;
using MeasurementSoftware.Services.UserSetting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace MeasurementSoftware.Services.Appearance
{
    public class AppAppearanceService : IAppAppearanceService
    {
        private const double DefaultFontSize = 14d;
        private const double MinFontSize = 10d;
        private const double MaxFontSize = 24d;
        private static readonly DependencyProperty OriginalFontSizeProperty = DependencyProperty.RegisterAttached(
            "OriginalFontSize",
            typeof(double),
            typeof(AppAppearanceService),
            new PropertyMetadata(double.NaN));
        private static readonly DependencyProperty OriginalFontFamilyProperty = DependencyProperty.RegisterAttached(
            "OriginalFontFamily",
            typeof(FontFamily),
            typeof(AppAppearanceService),
            new PropertyMetadata(null));

        private readonly IUserSettingsService _userSettingsService;
        private readonly List<WeakReference<FrameworkElement>> _attachedRoots = [];

        public AppAppearanceService(IUserSettingsService userSettingsService)
        {
            _userSettingsService = userSettingsService;
        }

        public string CurrentFontFamily => NormalizeFontFamily(_userSettingsService.Settings.Appearance).FontFamily;

        public double CurrentFontSize => NormalizeFontFamily(_userSettingsService.Settings.Appearance).FontSize;

        public void Initialize()
        {
            NormalizeFontFamily(_userSettingsService.Settings.Appearance);
            UpdateApplicationResources();
        }

        public void Attach(FrameworkElement element)
        {
            if (element == null)
            {
                return;
            }

            CleanupDeadReferences();
            if (_attachedRoots.Any(x => x.TryGetTarget(out var target) && ReferenceEquals(target, element)))
            {
                Apply(element);
                return;
            }

            _attachedRoots.Add(new WeakReference<FrameworkElement>(element));
            element.Loaded -= Element_Loaded;
            element.Loaded += Element_Loaded;

            if (element.IsLoaded)
            {
                Apply(element);
            }
        }

        public void UpdateFontSize(double fontSize)
        {
            var appearance = NormalizeFontFamily(_userSettingsService.Settings.Appearance);
            appearance.FontSize = Math.Clamp(fontSize, MinFontSize, MaxFontSize);
            UpdateApplicationResources();
            RefreshAttachedRoots();
        }

        private void Element_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                Apply(element);
            }
        }

        private void RefreshAttachedRoots()
        {
            CleanupDeadReferences();

            foreach (var weakReference in _attachedRoots)
            {
                if (weakReference.TryGetTarget(out var element))
                {
                    Apply(element);
                }
            }
        }

        private void CleanupDeadReferences()
        {
            _attachedRoots.RemoveAll(x => !x.TryGetTarget(out _));
        }

        private void UpdateApplicationResources()
        {
            var application = Application.Current;
            if (application == null)
            {
                return;
            }

            var appearance = NormalizeFontFamily(_userSettingsService.Settings.Appearance);
            application.Resources["GlobalAppFontFamily"] = new FontFamily(appearance.FontFamily);
            application.Resources["GlobalAppFontSize"] = appearance.FontSize;
        }

        private void Apply(FrameworkElement root)
        {
            var appearance = NormalizeFontFamily(_userSettingsService.Settings.Appearance);
            var scale = appearance.FontSize / DefaultFontSize;
            var fontFamily = new FontFamily(appearance.FontFamily);
            ApplyRootFont(root, fontFamily, appearance.FontSize);
            var visited = new HashSet<DependencyObject>();
            ApplyRecursive(root, fontFamily, scale, visited);
        }

        private static void ApplyRootFont(FrameworkElement root, FontFamily fontFamily, double fontSize)
        {
            switch (root)
            {
                case Control control:
                    control.FontFamily = fontFamily;
                    control.FontSize = fontSize;
                    break;
                case TextBlock textBlock:
                    textBlock.FontFamily = fontFamily;
                    textBlock.FontSize = fontSize;
                    break;
            }
        }

        private static void ApplyRecursive(DependencyObject? current, FontFamily fontFamily, double scale, HashSet<DependencyObject> visited)
        {
            if (current == null || !visited.Add(current))
            {
                return;
            }

            ApplyToCurrent(current, fontFamily, scale);

            if (current is Visual or Visual3D)
            {
                var visualChildrenCount = VisualTreeHelper.GetChildrenCount(current);
                for (var i = 0; i < visualChildrenCount; i++)
                {
                    ApplyRecursive(VisualTreeHelper.GetChild(current, i), fontFamily, scale, visited);
                }
            }

            foreach (var child in LogicalTreeHelper.GetChildren(current).OfType<DependencyObject>())
            {
                ApplyRecursive(child, fontFamily, scale, visited);
            }
        }

        private static void ApplyToCurrent(DependencyObject current, FontFamily fontFamily, double scale)
        {
            ApplyFontFamily(current, fontFamily);
            ApplyFontSize(current, scale);
        }

        private static void ApplyFontFamily(DependencyObject current, FontFamily targetFontFamily)
        {
            switch (current)
            {
                case Control control when control.ReadLocalValue(Control.FontFamilyProperty) != DependencyProperty.UnsetValue:
                    if (control.GetValue(OriginalFontFamilyProperty) is not FontFamily originalControlFont)
                    {
                        originalControlFont = control.FontFamily;
                        control.SetValue(OriginalFontFamilyProperty, originalControlFont);
                    }

                    if (!IsIconFontFamily(originalControlFont))
                    {
                        control.FontFamily = targetFontFamily;
                    }
                    break;
                case TextBlock textBlock when textBlock.ReadLocalValue(TextBlock.FontFamilyProperty) != DependencyProperty.UnsetValue:
                    if (textBlock.GetValue(OriginalFontFamilyProperty) is not FontFamily originalTextBlockFont)
                    {
                        originalTextBlockFont = textBlock.FontFamily;
                        textBlock.SetValue(OriginalFontFamilyProperty, originalTextBlockFont);
                    }

                    if (!IsIconFontFamily(originalTextBlockFont))
                    {
                        textBlock.FontFamily = targetFontFamily;
                    }
                    break;
                case TextElement textElement when textElement.ReadLocalValue(TextElement.FontFamilyProperty) != DependencyProperty.UnsetValue:
                    if (textElement.GetValue(OriginalFontFamilyProperty) is not FontFamily originalTextElementFont)
                    {
                        originalTextElementFont = textElement.FontFamily;
                        textElement.SetValue(OriginalFontFamilyProperty, originalTextElementFont);
                    }

                    if (!IsIconFontFamily(originalTextElementFont))
                    {
                        textElement.FontFamily = targetFontFamily;
                    }
                    break;
            }
        }

        private static void ApplyFontSize(DependencyObject current, double scale)
        {
            switch (current)
            {
                case Control control when control.ReadLocalValue(Control.FontSizeProperty) != DependencyProperty.UnsetValue:
                    control.FontSize = GetScaledFontSize(control, control.FontSize, scale);
                    break;
                case TextBlock textBlock when textBlock.ReadLocalValue(TextBlock.FontSizeProperty) != DependencyProperty.UnsetValue:
                    textBlock.FontSize = GetScaledFontSize(textBlock, textBlock.FontSize, scale);
                    break;
                case TextElement textElement when textElement.ReadLocalValue(TextElement.FontSizeProperty) != DependencyProperty.UnsetValue:
                    textElement.FontSize = GetScaledFontSize(textElement, textElement.FontSize, scale);
                    break;
            }
        }

        private static double GetScaledFontSize(DependencyObject current, double currentFontSize, double scale)
        {
            if (current.GetValue(OriginalFontSizeProperty) is not double originalFontSize || double.IsNaN(originalFontSize))
            {
                originalFontSize = currentFontSize;
                current.SetValue(OriginalFontSizeProperty, originalFontSize);
            }

            return Math.Round(originalFontSize * scale, 1, MidpointRounding.AwayFromZero);
        }

        private static bool IsIconFontFamily(FontFamily? fontFamily)
        {
            var source = fontFamily?.Source;
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            return source.Contains("iconfont", StringComparison.OrdinalIgnoreCase)
                   || source.Contains("mdl2", StringComparison.OrdinalIgnoreCase)
                   || source.Contains("assets", StringComparison.OrdinalIgnoreCase);
        }

        private static AppAppearanceSettings NormalizeFontFamily(AppAppearanceSettings appearance)
        {
            appearance.FontFamily = string.IsNullOrWhiteSpace(appearance.FontFamily)
                ? "Microsoft YaHei UI"
                : appearance.FontFamily;
            appearance.FontSize = double.IsFinite(appearance.FontSize)
                ? Math.Clamp(appearance.FontSize, MinFontSize, MaxFontSize)
                : DefaultFontSize;
            return appearance;
        }
    }
}
