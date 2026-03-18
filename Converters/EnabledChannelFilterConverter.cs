using MeasurementSoftware.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace MeasurementSoftware.Converters
{
    public class EnabledChannelFilterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value as IEnumerable<MeasurementChannel>)?.Where(c => c.IsEnabled).ToList()!;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
