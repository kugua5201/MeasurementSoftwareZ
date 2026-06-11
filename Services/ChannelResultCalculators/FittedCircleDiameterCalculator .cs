using MeasurementSoftware.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeasurementSoftware.Services.ChannelResultCalculators
{
    /// <summary>
    /// 拟合圆直径
    /// </summary>
    public sealed class FittedCircleDiameterCalculator : FittedCircleCalculatorBase
    {
        public override ChannelType ChannelType => ChannelType.拟合圆直径;

        protected override double ConvertRadius(double radius) => radius * 2d;
    }
}
