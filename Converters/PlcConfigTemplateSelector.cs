using MeasurementSoftware.Models;
using System.Windows;
using System.Windows.Controls;

namespace MeasurementSoftware.Converters
{
    public class PlcConfigTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? SiemensS7Template { get; set; }
        public DataTemplate? SiemensS71200Template { get; set; }
        public DataTemplate? SiemensS71500Template { get; set; }
        public DataTemplate? MitsubishiMCTemplate { get; set; }
        public DataTemplate? ModbusTCPTemplate { get; set; }
        public DataTemplate? ModbusRTUTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is PlcDevice device)
            {
                return device.DeviceType switch
                {
                    PlcDeviceType.SiemensS7_1200 => SiemensS71200Template ?? SiemensS7Template,
                    PlcDeviceType.SiemensS7_1500 => SiemensS71500Template ?? SiemensS7Template,
                    PlcDeviceType.MitsubishiMC => MitsubishiMCTemplate,
                    PlcDeviceType.ModbusTCP => ModbusTCPTemplate,
                    PlcDeviceType.ModbusRTU => ModbusRTUTemplate,
                    _ => null
                };
            }
            return null;
        }
    }
}
