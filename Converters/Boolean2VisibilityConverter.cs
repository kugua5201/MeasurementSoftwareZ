using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MeasurementSoftware.Converters
{
    /// <summary>
    /// 布尔值转可见性转换器
    /// true = Visible, false = Collapsed
    /// </summary>
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class Boolean2VisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }
            return false;
        }
    }
}
