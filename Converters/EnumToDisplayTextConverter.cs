using System.Globalization;
using System.Windows.Data;
using MeasurementSoftware.Extensions;

namespace MeasurementSoftware.Converters
{
    /// <summary>
    /// 枚举到显示文本的转换器（使用扩展方法）
    /// </summary>
    public class EnumToDisplayTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Enum enumValue)
            {
                return enumValue.GetDisplayName();
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
