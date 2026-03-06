using MeasurementSoftware.Models;
using System.Globalization;
using System.Windows.Data;

namespace MeasurementSoftware.Converters
{
    /// <summary>
    /// 标注显示文本转换器
    /// 参数: [0] ChannelNumber, [1] ChannelName, [2] StepNumber, [3] DisplayText(自定义), [4] AnnotationDisplayFormat
    /// </summary>
    public class AnnotationDisplayTextConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string? customText = values.Length >= 4 ? values[3] as string : null;
            if (!string.IsNullOrEmpty(customText)) return customText;

            var format = values.Length >= 5 && values[4] is AnnotationDisplayFormat f ? f : AnnotationDisplayFormat.通道编号;
            int channelNumber = values.Length >= 1 && values[0] is int cn ? cn : 0;
            string channelName = values.Length >= 2 ? values[1] as string ?? "" : "";
            int stepNumber = values.Length >= 3 && values[2] is int sn ? sn : 0;

            return format switch
            {
                AnnotationDisplayFormat.通道名称 => channelName,
                AnnotationDisplayFormat.工步编号 => $"S{stepNumber}",
                _ => channelNumber.ToString(),
            };
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
