using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Data;

namespace MeasurementSoftware.Converters
{
    /// <summary>
    /// 枚举描述转换器
    /// 将枚举值转换为其Description特性的值
    /// </summary>
    public class EnumDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            var type = value.GetType();
            if (!type.IsEnum)
                return value.ToString() ?? string.Empty;

            var fieldInfo = type.GetField(value.ToString() ?? string.Empty);
            if (fieldInfo == null)
                return value.ToString() ?? string.Empty;

            var attributes = fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false) as DescriptionAttribute[];

            if (attributes != null && attributes.Length > 0)
            {
                return attributes[0].Description;
            }

            return value.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || !targetType.IsEnum)
                return Binding.DoNothing;

            foreach (var field in targetType.GetFields())
            {
                var attributes = field.GetCustomAttributes(typeof(DescriptionAttribute), false) as DescriptionAttribute[];
                if (attributes != null && attributes.Length > 0)
                {
                    if (attributes[0].Description == value.ToString())
                    {
                        return field.GetValue(null) ?? Binding.DoNothing; ;
                    }
                }

                if (field.Name == value.ToString())
                {
                    return field.GetValue(null) ?? Binding.DoNothing; ;
                }
            }

            return Binding.DoNothing;
        }
    }
}
