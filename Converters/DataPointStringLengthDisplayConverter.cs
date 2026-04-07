using MeasurementSoftware.Models;
using MultiProtocol.Model;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MeasurementSoftware.Converters
{
    /// <summary>
    /// 数据点字符串长度显示转换器。
    /// 西门子设备或非字符串类型统一显示“无需设置”，仅其他设备的字符串类型显示实际长度。
    /// </summary>
    public class DataPointStringLengthDisplayConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var dataLength = values.Length > 0 && values[0] is int length ? length : 0;
            var dataType = values.Length > 1 && values[1] is FieldType fieldType ? fieldType : (FieldType?)null;
            var deviceType = values.Length > 2 && values[2] is PlcDeviceType plcDeviceType ? plcDeviceType : (PlcDeviceType?)null;

            if (dataType != FieldType.String || deviceType is PlcDeviceType.SiemensS7_1200 or PlcDeviceType.SiemensS7_1500)
            {
                return "-";
            }

            return dataLength > 0 ? dataLength.ToString(culture) : string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            var results = new object[targetTypes.Length];
            for (int i = 0; i < results.Length; i++)
            {
                results[i] = Binding.DoNothing;
            }

            if (value is string text && int.TryParse(text, NumberStyles.Integer, culture, out var length) && length > 0)
            {
                results[0] = length;
            }

            return results;
        }
    }
}
