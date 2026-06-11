using MeasurementSoftware.Extensions;
using MeasurementSoftware.Models;
using System.Diagnostics;

namespace MeasurementSoftware.Services.MeasurementValueFilters
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

        public List<HistoryRecordModel> Apply(IReadOnlyList<HistoryRecordModel> records, MeasurementChannel channel)
        {
            if (records.Count == 0)
            {
                return [];
            }

            int windowSize = GetEffectiveFilterSampleCount(channel);
            var result = new List<HistoryRecordModel>(records.Count);

            foreach (var record in records)
            {
                if (!record.X.HasValue || record.YValues.Count == 0)
                {
                    result.Add(new HistoryRecordModel(record.X, record.YValues));
                    continue;
                }

                if (record.YValues.Count < windowSize)
                {
                    result.Add(new HistoryRecordModel(record.X, record.YValues));
                    continue;
                }

                var filteredYValues = new List<double>(record.YValues);
                int halfWindow = windowSize / 2;

                for (int centerIndex = halfWindow; centerIndex < record.YValues.Count - halfWindow; centerIndex++)
                {
                    double sum = 0d;
                    for (int i = centerIndex - halfWindow; i <= centerIndex + halfWindow; i++)
                    {
                        sum += record.YValues[i];
                    }

                    filteredYValues[centerIndex] = Math.Round(sum / windowSize, channel.DecimalPlaces);
                }

                result.Add(new HistoryRecordModel(record.X, filteredYValues));
            }

            return result;
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
