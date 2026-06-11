using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeasurementSoftware.Models
{
    public sealed class HistoryRecordModel
    {
        public double? X { get; set; }

        public List<double> YValues { get; set; } = new();

        public HistoryRecordModel()
        {
        }

        public HistoryRecordModel(double? x, IEnumerable<double> yValues)
        {
            X = x;
            YValues = yValues.ToList();
        }
    }
}
