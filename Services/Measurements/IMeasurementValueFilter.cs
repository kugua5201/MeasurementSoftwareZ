using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services.Measurements
{
    /// <summary>
    /// 测量值滤波器。
    /// </summary>
    public interface IMeasurementValueFilter
    {
        /// <summary>
        /// 当前滤波器支持的类型。
        /// </summary>
        MeasurementFilterType FilterType { get; }

        /// <summary>
        /// 根据历史数据和通道配置计算滤波结果。
        /// </summary>
        /// <param name="values">待处理的历史样本数据。</param>
        /// <param name="channel">通道配置模型。</param>
        /// <returns>滤波后的整段结果。</returns>
        List<double> Apply(IReadOnlyList<double> values, MeasurementChannel channel);
    }
}
