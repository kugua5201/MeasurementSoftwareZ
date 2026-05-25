using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services.Measurements
{
    /// <summary>
    /// 测量值滤波器静态工厂。
    /// </summary>
    public static class MeasurementValueFilterFactory
    {
        /// <summary>
        /// 按滤波类型创建对应实现。
        /// </summary>
        /// <param name="filterType">滤波类型。</param>
        /// <returns>滤波器实现。</returns>
        /// <exception cref="NotSupportedException">不支持的滤波类型。</exception>
        public static IMeasurementValueFilter Create(MeasurementFilterType filterType)
        {
            return filterType switch
            {
                MeasurementFilterType.Average => new AverageMeasurementValueFilter(),
                _ => throw new NotSupportedException($"不支持的滤波类型：{filterType}")
            };
        }
    }
}
