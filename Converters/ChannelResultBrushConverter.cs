using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using MeasurementSoftware.Models;

namespace MeasurementSoftware.Converters
{
    /// <summary>
    /// 通道结果背景色转换器。
    /// 等待/未测量时返回浅灰，采集中/OK/NG 返回对应配置颜色。
    /// </summary>
    public class ChannelResultBrushConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string? okColor = values.Length >= 1 ? values[0] as string : null;
            string? ngColor = values.Length >= 2 ? values[1] as string : null;
            string? acquiringColor = values.Length >= 3 ? values[2] as string : null;
            var displayState = values.Length >= 4 && values[3] is MeasurementResult state
                ? state
                : MeasurementResult.Waiting;

            return displayState switch
            {
                MeasurementResult.Pass => TryParseBrush(okColor) ?? Application.Current.FindResource("StatusSuccessBrush") as Brush ?? Brushes.Green,
                MeasurementResult.Fail => TryParseBrush(ngColor) ?? Application.Current.FindResource("StatusErrorBrush") as Brush ?? Brushes.Red,
                MeasurementResult.Acquiring => TryParseBrush(acquiringColor) ?? Application.Current.FindResource("StatusWarningBrush") as Brush ?? Brushes.Orange,
                _ => Application.Current.FindResource("DisabledIconBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(189, 189, 189))
            };
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static SolidColorBrush? TryParseBrush(string? colorStr)
        {
            if (string.IsNullOrWhiteSpace(colorStr))
            {
                return null;
            }

            try
            {
                var color = (Color)ColorConverter.ConvertFromString(colorStr);
                return new SolidColorBrush(color);
            }
            catch
            {
                return null;
            }
        }
    }
}
