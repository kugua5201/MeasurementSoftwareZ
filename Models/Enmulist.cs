using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace MeasurementSoftware.Models
{
    /// <summary>
    /// PLC设备类型
    /// </summary>
    public enum PlcDeviceType
    {
        SiemensS7_S1200,      // 西门子S7
        MitsubishiMC,   // 三菱MC
        ModbusTCP,      // Modbus-TCP
        ModbusRTU       // Modbus-RTU
    }

    /// <summary>
    /// 二维码数据源类型
    /// </summary>
    public enum QrCodeSourceType
    {
        [Description("键盘输入")]
        KeyboardInput,

        [Description("串口通信")]
        SerialPort,

        [Description("以太网通信")]
        Ethernet,

        [Description("PLC寄存器")]
        PlcRegister
    }


    /// <summary>
    /// 通道类型枚举
    /// </summary>
    public enum ChannelType
    {
        /// <summary>
        /// 结果值：测量完成之后读取的最终值
        /// </summary>
        结果值,

        /// <summary>
        /// 最大值
        /// </summary>
        最大值,

        /// <summary>
        /// 最小值
        /// </summary>
        最小值,

        /// <summary>
        /// 平均值
        /// </summary>
        平均值,

        /// <summary>
        /// 跳动值
        /// </summary>
        跳动值,

        /// <summary>
        /// 齿跳动值
        /// </summary>
        齿跳动值
    }

    /// <summary>
    /// 测量结果枚举
    /// </summary>
    public enum MeasurementResult
    {
        /// <summary>
        /// 未测量
        /// </summary>
        NotMeasured,

        /// <summary>
        /// 合格
        /// </summary>
        Pass,

        /// <summary>
        /// 不合格
        /// </summary>
        Fail
    }
}
