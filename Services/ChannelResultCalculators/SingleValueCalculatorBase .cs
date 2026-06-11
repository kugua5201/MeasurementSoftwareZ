using MeasurementSoftware.Extensions;
using MeasurementSoftware.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeasurementSoftware.Services.ChannelResultCalculators
{
    /// <summary>
    ///一维结果的公共基类
    /// </summary>
    public abstract class SingleValueCalculatorBase : IChannelResultCalculator
    {
        public abstract ChannelType ChannelType { get; }

        public double Calculate(IReadOnlyList<HistoryRecordModel> records, MeasurementChannel channel)
        {
            var values = records.FlattenHistoryValues();
            if (values.Count == 0)
            {
                throw new InvalidOperationException("没有采集到数据");
            }

            return CalculateCore(values, channel);
        }

        protected abstract double CalculateCore(IReadOnlyList<double> values, MeasurementChannel channel);
    }
}
