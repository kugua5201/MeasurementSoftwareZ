using System.ComponentModel;

namespace MeasurementSoftware.Models
{
    /// <summary>
    /// 波特率枚举
    /// </summary>
    public enum BaudRate
    {
        [Description("300")]
        Baud300 = 300,
        [Description("600")]
        Baud600 = 600,
        [Description("1200")]
        Baud1200 = 1200,
        [Description("2400")]
        Baud2400 = 2400,
        [Description("4800")]
        Baud4800 = 4800,
        [Description("9600")]
        Baud9600 = 9600,
        [Description("19200")]
        Baud19200 = 19200,
        [Description("38400")]
        Baud38400 = 38400,
        [Description("57600")]
        Baud57600 = 57600,
        [Description("115200")]
        Baud115200 = 115200
    }

    /// <summary>
    /// 数据位枚举
    /// </summary>
    public enum DataBits
    {
        [Description("5位")]
        Five = 5,
        [Description("6位")]
        Six = 6,
        [Description("7位")]
        Seven = 7,
        [Description("8位")]
        Eight = 8
    }

    /// <summary>
    /// 网络协议类型枚举
    /// </summary>
    public enum NetworkProtocol
    {
        [Description("TCP")]
        TCP,
        [Description("UDP")]
        UDP
    }
}
