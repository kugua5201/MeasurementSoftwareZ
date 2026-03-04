using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MeasurementSoftware.Converters
{
    /// <summary>
    /// 字符串转可见性转换器
    /// 非空字符串显示，空字符串隐藏
    /// </summary>
    public class String2VisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return string.IsNullOrEmpty(str) ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
