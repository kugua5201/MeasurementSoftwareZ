using MeasurementSoftware.Models;
using System.Diagnostics;

namespace MeasurementSoftware.Services.Measurements
{
    /// <summary>
    /// 平均滤波。
    /// 对窗口内最近 N 个样本求平均值作为当前输出。
    /// </summary>
    public sealed class AverageMeasurementValueFilter : IMeasurementValueFilter
    {
        /// <summary>
        /// 当前实现对应平均滤波。
        /// </summary>
        public MeasurementFilterType FilterType => MeasurementFilterType.Average;

        /// <summary>
        /// 基于历史数据执行平均滤波。
        /// </summary>
        /// <param name="values">待处理的历史样本数据。</param>
        /// <param name="channel">通道配置模型。</param>
        /// <returns>平均滤波后的整段结果。</returns>
        public List<double> Apply(IReadOnlyList<double> values, MeasurementChannel channel)
        {
            if (values.Count == 0)
            {
                return [];
            }

            // 平均滤波采用“居中窗口替换”规则：
            // 3 点滤波时用 [1,2,3] 的平均值替换中间点 2；
            // 5 点滤波时用 [1,2,3,4,5] 的平均值替换中间点 3。
            // 头尾两侧因窗口不完整，不做替换，保持原值。
            int effectiveFilterSampleCount = channel.FilterSampleCount;
            List<double> filteredValues = [.. values];
            if (filteredValues.Count < effectiveFilterSampleCount)
            {
                return filteredValues;
            }

            int halfWindow = effectiveFilterSampleCount / 2;

            for (int centerIndex = halfWindow; centerIndex < values.Count - halfWindow; centerIndex++)
            {
                double sum = 0d;
                int startIndex = centerIndex - halfWindow;
                int endIndex = centerIndex + halfWindow;

                for (int i = startIndex; i <= endIndex; i++)
                {
                    sum += values[i];
                }

                filteredValues[centerIndex] = Math.Round(sum / effectiveFilterSampleCount, channel.DecimalPlaces);
            }
            Debug.WriteLine("原始值: " + string.Join(", ", values.Select(v => v.ToString())));
            Debug.WriteLine("滤波后: " + string.Join(", ", filteredValues.Select(v => v.ToString())));
            return filteredValues;
        }

        private static int GetEffectiveFilterSampleCount(MeasurementChannel channel)
        {
            int maxFilterSampleCount = Math.Max(3, channel.SampleCount / 2);
            if (maxFilterSampleCount % 2 == 0)
            {
                maxFilterSampleCount--;
            }

            int effectiveFilterSampleCount = Math.Clamp(channel.FilterSampleCount, 3, maxFilterSampleCount);
            if (effectiveFilterSampleCount % 2 == 0)
            {
                effectiveFilterSampleCount--;
            }

            return effectiveFilterSampleCount;
        }
    }
}
