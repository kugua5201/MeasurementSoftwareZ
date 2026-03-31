using System.Globalization;
using System.Windows.Data;

namespace MeasurementSoftware.Converters
{
    /// <summary>
    /// 通道显示文本转换器。
    /// 将配置的前缀和通道数字拼接为最终显示编号。
    /// </summary>
    public class ChannelDisplayTextConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var prefix = values.Length >= 1 ? values[0] as string ?? string.Empty : string.Empty;
            var channelNumber = values.Length >= 2 && values[1] is int number ? number : 0;

            return string.IsNullOrWhiteSpace(prefix)
                ? channelNumber.ToString(culture)
                : $"{prefix}{channelNumber}";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
