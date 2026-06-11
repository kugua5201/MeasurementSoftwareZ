using MeasurementSoftware.Extensions;
using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services.MeasurementValueFilters
{
    /// <summary>
    /// 高速滤波。
    /// 使用较高权重的一阶指数平滑，在抑制抖动的同时尽量保留快速响应能力。
    /// </summary>
    public sealed class HighSpeedMeasurementValueFilter : IMeasurementValueFilter
    {
        public MeasurementFilterType FilterType => MeasurementFilterType.HighSpeed;

        public List<HistoryRecordModel> Apply(IReadOnlyList<HistoryRecordModel> records, MeasurementChannel channel)
        {
            if (records.Count == 0)
            {
                return [];
            }

            double alpha = Math.Clamp(channel.HighSpeedAlpha, 0d, 1d);
            var result = new List<HistoryRecordModel>(records.Count);

            double? previous = null;

            foreach (var record in records)
            {
                if (!record.X.HasValue || record.YValues.Count == 0)
                {
                    result.Add(new HistoryRecordModel(record.X, record.YValues));
                    continue;
                }

                var filteredYValues = new List<double>(record.YValues.Count);

                foreach (var y in record.YValues)
                {
                    double current = previous == null
                        ? Math.Round(y, channel.DecimalPlaces)
                        : Math.Round((alpha * y) + ((1d - alpha) * previous.Value), channel.DecimalPlaces);

                    filteredYValues.Add(current);
                    previous = current;
                }

                result.Add(new HistoryRecordModel(record.X, filteredYValues));
            }

            return result;
        }
    }
}
