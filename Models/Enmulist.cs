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
    /// 测量通道模式。
    /// </summary>
    public enum MeasurementChannelMode
    {
        [Description("直接测量")]
        Direct,

        [Description("间接测量")]
        Indirect,

        [Description("虚拟通道")]
        Virtual
    }

    /// <summary>
    /// 间接测量触发模式。
    /// 用于控制公式在运行时按什么规则触发一次计算与存储。
    /// </summary>
    public enum IndirectMeasurementTriggerMode
    {
        /// <summary>
        /// 绑定点位值事件一到就计算一次。
        /// 不区分公式变量值是否真的发生变化。
        /// </summary>
        [Description("事件触发")]
        EventReceived,

        /// <summary>
        /// 公式中任意一个变量值变化就触发计算。
        /// </summary>
        [Description("任意变化触发")]
        AnyValueChanged,

        /// <summary>
        /// 公式中所有变量至少都发生过一次变化后才触发计算。
        /// 每完成一轮后重新开始累计。
        /// </summary>
        [Description("全部变化触发")]
        AllValuesChanged
    }

    /// <summary>
    /// 虚拟测量数据来源模式。
    /// </summary>
    public enum VirtualMeasurementSourceMode
    {
        /// <summary>
        /// 软件模拟数据。
        /// </summary>
        [Description("软件模拟数据")]
        SoftwareSimulation,

        /// <summary>
        /// 基于其他测量通道结果的公式计算。
        /// </summary>
        [Description("测量通道计算")]
        ChannelFormula
    }

    /// <summary>
    /// 软件模拟数据波形类型。
    /// </summary>
    public enum VirtualMeasurementWaveformType
    {
        /// <summary>
        /// 正弦波。
        /// </summary>
        [Description("正弦波")]
        Sine,

        /// <summary>
        /// 方波。
        /// </summary>
        [Description("方波")]
        Square
    }

    /// <summary>
    /// 测量结果枚举
    /// </summary>
    public enum MeasurementResult
    {
        /// <summary>
        /// 未测量
        /// </summary>
        [Description("未测量")]
        NotMeasured,
        /// <summary>
        /// 等待测量。
        /// 主要用于界面显示状态。
        /// </summary>
        [Description("等待")]
        Waiting,
        /// <summary>
        /// 正在采集中。
        /// 主要用于界面显示状态。
        /// </summary>
        [Description("采集中")]
        Acquiring,
        /// <summary>
        /// 合格
        /// </summary>
        [Description("OK")]
        /// <summary>
        /// 不合格
        /// </summary>
        Pass,
        [Description("NG")]
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
    /// 工步操作类型。
    /// </summary>
    public enum StepOperationType
    {
        [Description("开始测量")]
        StartAcquisition,

        [Description("完成测量")]
        StopAcquisition,

        [Description("终止测量")]
        TerminateMeasurement,

        [Description("上一步")]
        PreviousStep,

        [Description("下一步")]
        NextStep,

       
    }

    /// <summary>
    /// 工步操作触发方式。
    /// </summary>
    public enum StepOperationTriggerMode
    {
        [Description("值等于")]
        ValueEquals,

        [Description("上升沿")]
        RisingEdge,

        [Description("下降沿")]
        FallingEdge,

        [Description("值变化")]
        AnyChange
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
