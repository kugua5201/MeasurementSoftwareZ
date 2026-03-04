using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MeasurementSoftware.Converters
{
    /// <summary>
    /// 布尔值转颜色转换器
    /// true 转为绿色，false 转为红色（可以通过参数自定义）
    /// </summary>
    [ValueConversion(typeof(bool), typeof(Brush))]
    public class Boolean2ColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                if (parameter is string colorPair)
                {
                    var colors = colorPair.Split('|');
                    if (colors.Length == 2)
                    {
                        return boolValue 
                            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[0]))
                            : new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[1]));
                    }
                }

                // 默认：true = 绿色, false = 红色
                return boolValue ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
