using System;
using System.Globalization;
using System.Windows.Data;

namespace MeasurementSoftware.Converters
{
    /// <summary>
    /// 根据采样数量生成滤波点数提示文本。
    /// 提示文案放在界面层，避免 MeasurementChannel 承担展示职责。
    /// </summary>
    [ValueConversion(typeof(int), typeof(string))]
    public sealed class FilterSampleCountHintConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int halfSampleCount = 3;
            if (value is int sampleCount)
            {
                halfSampleCount = Math.Max(3, sampleCount / 2);
                if (halfSampleCount % 2 == 0)
                {
                    halfSampleCount--;
                }

                return $"需填写 3 ~ {halfSampleCount} 的奇数";
            }

            return "需填写不小于 3 的奇数";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
