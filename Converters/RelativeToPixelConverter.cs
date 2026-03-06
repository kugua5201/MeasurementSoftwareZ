using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MeasurementSoftware.Converters
{
    /// <summary>
    /// 将相对坐标（0~1）转换为实际像素坐标
    /// Parameters: value=相对比例, parameter=容器实际尺寸(ActualWidth/ActualHeight)
    /// </summary>
    public class RelativeToPixelConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2
                && values[0] is double ratio
                && values[1] is double size)
            {
                return ratio * size;
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
