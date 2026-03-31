using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MeasurementSoftware.Converters
{
    /// <summary>
    /// 标注颜色转换器（多值）
    /// 参数: [0] OkColor, [1] NgColor, [2] DefaultColor, [3] AcquiringColor, [4] DisplayState, [5] ForceDefaultColor
    /// 根据显示状态返回配方级别的等待/采集中/OK/NG颜色
    /// </summary>
    public class AnnotationBrushConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string? okColor = values.Length >= 1 ? values[0] as string : null;
            string? ngColor = values.Length >= 2 ? values[1] as string : null;
            string? defaultColor = values.Length >= 3 ? values[2] as string : null;
            string? acquiringColor = values.Length >= 4 ? values[3] as string : null;
            var displayState = values.Length >= 5 && values[4] is Models.MeasurementResult state
                ? state
                : Models.MeasurementResult.Waiting;
            bool forceDefault = values.Length >= 6 && values[5] is true;
            if (forceDefault)
            {
                displayState = Models.MeasurementResult.Waiting;
            }

            return displayState switch
            {
                Models.MeasurementResult.Pass => TryParseBrush(okColor) ?? Application.Current.FindResource("StatusSuccessBrush") as Brush ?? Brushes.Green,
                Models.MeasurementResult.Fail => TryParseBrush(ngColor) ?? Application.Current.FindResource("StatusErrorBrush") as Brush ?? Brushes.Red,
                Models.MeasurementResult.Acquiring => TryParseBrush(acquiringColor) ?? Application.Current.FindResource("StatusWarningBrush") as Brush ?? Brushes.Orange,
                _ => TryParseBrush(defaultColor) ?? Application.Current.FindResource("StatusInfoBrush") as Brush ?? Brushes.DodgerBlue,
            };
        }

        private static SolidColorBrush? TryParseBrush(string? colorStr)
        {
            if (string.IsNullOrEmpty(colorStr)) return null;
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(colorStr);
                return new SolidColorBrush(color);
            }
            catch { return null; }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
