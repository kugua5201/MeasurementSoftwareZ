using MeasurementSoftware.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeasurementSoftware.Services.ChannelResultCalculators
{
    /// <summary>
    /// 最小值
    /// </summary>
    public sealed class MinValueCalculator : SingleValueCalculatorBase
    {
        public override ChannelType ChannelType => ChannelType.最小值;

        protected override double CalculateCore(IReadOnlyList<double> values, MeasurementChannel channel)
            => values.Min();
    }
}
