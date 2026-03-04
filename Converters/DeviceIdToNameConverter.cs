using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using Autofac;
using MeasurementSoftware.Extensions;
using MeasurementSoftware.Services.Config;

namespace MeasurementSoftware.Converters
{
    /// <summary>
    /// 设备ID转设备名称转换器
    /// </summary>
    public class DeviceIdToNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long deviceId && deviceId != 0)
            {
                try
                {
                    var deviceService = ContainerBuilderExtensions.GetService<IDeviceConfigService>();
                    var device = deviceService?.Devices.FirstOrDefault(d => d.DeviceId == deviceId);
                    return device?.DeviceName ?? deviceId.ToString();
                }
                catch
                {
                    return deviceId.ToString();
                }
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
