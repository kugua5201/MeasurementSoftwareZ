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
    /// 最大值
    /// </summary>
    public sealed class MaxValueCalculator : SingleValueCalculatorBase
    {
        public override ChannelType ChannelType => ChannelType.最大值;

        protected override double CalculateCore(IReadOnlyList<double> values, MeasurementChannel channel)
            => values.Max();
    }

}
