namespace MeasurementSoftware.Models
{
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
}
