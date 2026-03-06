using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MeasurementSoftware.Converters
{
    /// <summary>
    /// 测量结果转标注颜色：NotMeasured=蓝, Pass=绿, Fail=红
    /// </summary>
    public class ResultToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Models.MeasurementResult result)
            {
                return result switch
                {
                    Models.MeasurementResult.Pass => Application.Current.FindResource("StatusSuccessBrush") as Brush ?? Brushes.Green,
                    Models.MeasurementResult.Fail => Application.Current.FindResource("StatusErrorBrush") as Brush ?? Brushes.Red,
                    _ => Application.Current.FindResource("StatusInfoBrush") as Brush ?? Brushes.DodgerBlue,
                };
            }
            return Application.Current.FindResource("StatusInfoBrush") as Brush ?? Brushes.DodgerBlue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
