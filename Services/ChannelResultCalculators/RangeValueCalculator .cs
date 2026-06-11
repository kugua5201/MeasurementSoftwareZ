using MeasurementSoftware.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeasurementSoftware.Services.ChannelResultCalculators
{

    /// <summary>
    /// 跳动值
    /// </summary>
    public sealed class RangeValueCalculator : SingleValueCalculatorBase
    {
        public override ChannelType ChannelType => ChannelType.跳动值;

        protected override double CalculateCore(IReadOnlyList<double> values, MeasurementChannel channel)
            => values.Max() - values.Min();
    }
}
