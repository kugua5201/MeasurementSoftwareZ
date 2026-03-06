using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MeasurementSoftware.Converters
{
    /// <summary>
    /// 标注大小转偏移 Margin 转换器
    /// 将标注大小转为 Thickness(-size/2, -size/2, 0, 0)，使标注中心对齐坐标点
    /// </summary>
    public class SizeToOffsetMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double size && size > 0)
            {
                double offset = -size / 2;
                return new Thickness(offset, offset, 0, 0);
            }
            return new Thickness(-14, -14, 0, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
