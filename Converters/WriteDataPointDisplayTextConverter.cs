using MeasurementSoftware.Models;
using System.Globalization;
using System.Windows.Data;

namespace MeasurementSoftware.Converters
{
    /// <summary>
    /// 按写入点位配置生成标签模式显示文本。
    /// </summary>
    public class WriteDataPointDisplayTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not WriteDataPointConfig config)
            {
                return "--";
            }

            return string.IsNullOrWhiteSpace(config.CurrentValueDisplayText) ? "--" : config.CurrentValueDisplayText;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}