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
    /// 拟合圆半径
    /// </summary>
    public sealed class FittedCircleRadiusCalculator : FittedCircleCalculatorBase
    {

        public override ChannelType ChannelType => ChannelType.拟合圆半径;
    }
}
