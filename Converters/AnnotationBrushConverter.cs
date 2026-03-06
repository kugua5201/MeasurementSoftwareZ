using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MeasurementSoftware.Converters
{
    /// <summary>
    /// 标注颜色转换器（多值）
    /// 参数: [0] OkColor (string), [1] NgColor (string), [2] DefaultColor (string), [3] Result (MeasurementResult)
    /// 根据测量结果返回配方级别的OK/NG/默认颜色
    /// </summary>
    public class AnnotationBrushConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string? okColor = values.Length >= 1 ? values[0] as string : null;
            string? ngColor = values.Length >= 2 ? values[1] as string : null;
            string? defaultColor = values.Length >= 3 ? values[2] as string : null;
            var result = values.Length >= 4 && values[3] is Models.MeasurementResult r ? r : Models.MeasurementResult.NotMeasured;
            bool forceDefault = values.Length >= 5 && values[4] is true;
            if (forceDefault) result = Models.MeasurementResult.NotMeasured;

            return result switch
            {
                Models.MeasurementResult.Pass => TryParseBrush(okColor) ?? Application.Current.FindResource("StatusSuccessBrush") as Brush ?? Brushes.Green,
                Models.MeasurementResult.Fail => TryParseBrush(ngColor) ?? Application.Current.FindResource("StatusErrorBrush") as Brush ?? Brushes.Red,
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
