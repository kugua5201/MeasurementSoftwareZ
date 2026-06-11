using MeasurementSoftware.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeasurementSoftware.Services.ChannelResultCalculators
{
    /// <summary>
    /// 平均值
    /// </summary>
    public sealed class AverageValueCalculator : SingleValueCalculatorBase
    {
        public override ChannelType ChannelType => ChannelType.平均值;

        protected override double CalculateCore(IReadOnlyList<double> values, MeasurementChannel channel)
            => values.Average();
    }

}
