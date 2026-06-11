using MeasurementSoftware.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeasurementSoftware.Services.ChannelResultCalculators
{
    public static class ChannelResultCalculatorFactory
    {
        public static IChannelResultCalculator Create(ChannelType channelType)
        {
            return channelType switch
            {
                ChannelType.结果值 => new LatestValueCalculator(),
                ChannelType.最大值 => new MaxValueCalculator(),
                ChannelType.最小值 => new MinValueCalculator(),
                ChannelType.平均值 => new AverageValueCalculator(),
                ChannelType.跳动值 => new RangeValueCalculator(),
                ChannelType.齿跳动值 => new ToothJumpValueCalculator(),
                ChannelType.拟合圆半径 => new FittedCircleRadiusCalculator(),
                ChannelType.拟合圆直径 => new FittedCircleDiameterCalculator(),
                _ => throw new NotSupportedException($"不支持的结果类型: {channelType}")
            };
        }
    }
}
