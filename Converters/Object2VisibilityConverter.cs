using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MeasurementSoftware.Converters
{
    /// <summary>
    /// 对象转可见性转换器
    /// null -> Collapsed
    /// not null -> Visible
    /// </summary>
    public class Object2VisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
