using MeasurementSoftware.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeasurementSoftware.Extensions
{
    public static class HistoryPointsExtensions
    {
        /// <summary>
        /// 展开二维点
        /// </summary>
        /// <param name="records"></param>
        /// <returns></returns>
        public static List<(double X, double Y)> FlattenPoints(this IEnumerable<HistoryRecordModel> records)
        {
            var points = new List<(double X, double Y)>();

            foreach (var record in records)
            {
                if (!record.X.HasValue)
                {
                    record.X = 0;
                }

                foreach (var y in record.YValues)
                {
                    points.Add((record.X.Value, y));
                }
            }

            return points;
        }



        /// <summary>
        /// 构建二维点列表
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>

        public static List<HistoryRecordModel> RebuildRecords(this IReadOnlyList<(double X, double Y)> points)
        {
            var result = new List<HistoryRecordModel>();
            HistoryRecordModel? current = null;
            double? currentX = null;

            foreach (var point in points)
            {
                if (current == null || currentX != point.X)
                {
                    current = new HistoryRecordModel
                    {
                        X = point.X
                    };
                    result.Add(current);
                    currentX = point.X;
                }

                current.YValues.Add(point.Y);
            }

            return result;
        }
        /// <summary>
        /// 克隆记录列表，避免修改原始数据
        /// </summary>
        /// <param name="records"></param>
        /// <returns></returns>
        public static List<HistoryRecordModel> CloneRecords(this IReadOnlyList<HistoryRecordModel> records)
        {
            return records.Select(r => new HistoryRecordModel(r.X, r.YValues)).ToList();
        }
        /// <summary>
        /// 展开二维点的 Y 值列表，得到一个纯粹的数值列表
        /// </summary>
        /// <param name="records"></param>
        /// <returns></returns>
        public static List<double> FlattenHistoryValues(this IEnumerable<HistoryRecordModel> records)
        {
            return records.SelectMany(record => record.YValues).ToList();
        }
    }
}
