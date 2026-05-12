using MeasurementSoftware.Models;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MeasurementSoftware.Converters
{
    /// <summary>
    /// 根据写入点位交互模式返回可见性。
    /// </summary>
    public class WriteDataPointEditorModeVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not WriteValueEditorMode mode || parameter is not string parameterText)
            {
                return Visibility.Collapsed;
            }

            return Enum.TryParse<WriteValueEditorMode>(parameterText, true, out var expectedMode) && expectedMode == mode
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}