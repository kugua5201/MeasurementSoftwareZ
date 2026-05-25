using System;
using System.Globalization;
using System.Windows.Data;

namespace MeasurementSoftware.Converters
{
    /// <summary>
    /// 根据采样数量计算滤波点数输入框允许的最大值。
    /// 该逻辑仅用于界面展示，实际滤波时仍由滤波实现类按通道配置自行处理。
    /// </summary>
    [ValueConversion(typeof(int), typeof(int))]
    public sealed class FilterSampleCountMaximumConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not int sampleCount)
            {
                return 3;
            }

            int halfSampleCount = Math.Max(3, sampleCount / 2);
            return halfSampleCount % 2 == 0 ? halfSampleCount - 1 : halfSampleCount;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
