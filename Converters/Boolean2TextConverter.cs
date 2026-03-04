using System.Globalization;
using System.Windows.Data;

namespace MeasurementSoftware.Converters
{
    /// <summary>
    /// 布尔值转文本转换器
    /// 参数格式：trueText|falseText
    /// </summary>
    [ValueConversion(typeof(bool), typeof(string))]
    public class Boolean2TextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string textPair)
            {
                var texts = textPair.Split('|');
                if (texts.Length == 2)
                {
                    return boolValue ? texts[0] : texts[1];
                }
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
