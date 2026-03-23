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
        SiemensS7_1200,
        SiemensS7_1500,
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
        [Description("未测量")]
        /// <summary>
        /// 未测量
        /// </summary>
        NotMeasured,

        [Description("OK")]
        /// <summary>
        /// 合格
        /// </summary>
        Pass,
        [Description("NG")]
        /// <summary>
        /// 不合格
        /// </summary>
        Fail
    }

    /// <summary>
    /// 标注形状枚举
    /// </summary>
    public enum AnnotationShape
    {
        /// <summary>
        /// 圆形
        /// </summary>
        圆形,

        /// <summary>
        /// 方形
        /// </summary>
        方形,

        /// <summary>
        /// 菱形
        /// </summary>
        菱形
    }

    /// <summary>
    /// 标注显示内容格式
    /// </summary>
    public enum AnnotationDisplayFormat
    {
        通道编号,
        通道名称,
        工步编号
    }


    /// <summary>
    /// 上传状态枚举
    /// </summary>
    public enum UploadStatus
    {
        /// <summary>
        /// 待上传
        /// </summary>
        Pending,

        /// <summary>
        /// 上传中
        /// </summary>
        Uploading,

        /// <summary>
        /// 上传成功
        /// </summary>
        Success,

        /// <summary>
        /// 上传失败
        /// </summary>
        Failed
    }
}
