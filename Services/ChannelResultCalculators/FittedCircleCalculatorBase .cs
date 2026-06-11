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
    /// 拟合圆公共基类
    /// </summary>
    public abstract class FittedCircleCalculatorBase : IChannelResultCalculator
    {
        public abstract ChannelType ChannelType { get; }

        public double Calculate(IReadOnlyList<HistoryRecordModel> records, MeasurementChannel channel)
        {
            var points = records.FlattenPoints();
            var radius = CalculateFittedCircleRadius(points);
            return ConvertRadius(radius);
        }

        protected virtual double ConvertRadius(double radius) => radius;

        protected double CalculateFittedCircleRadius(List<(double X, double Y)> points)
        {
            if (points.Count < 3)
            {
                throw new InvalidOperationException("拟合圆至少需要 3 个有效点。");
            }

            double xAvg = points.Average(p => p.X);
            double yAvg = points.Average(p => p.Y);

            double suu = 0d, suv = 0d, svv = 0d;
            double suuu = 0d, svvv = 0d, suvv = 0d, svuu = 0d;

            foreach (var (x, y) in points)
            {
                double u = x - xAvg;
                double v = y - yAvg;

                double uu = u * u;
                double vv = v * v;

                suu += uu;
                suv += u * v;
                svv += vv;
                suuu += uu * u;
                svvv += vv * v;
                suvv += u * vv;
                svuu += v * uu;
            }

            double denominator = 2d * (suu * svv - suv * suv);
            if (Math.Abs(denominator) < 1e-12)
            {
                throw new InvalidOperationException("拟合圆失败，点可能共线。");
            }

            double uc = (svv * (suuu + suvv) - suv * (svvv + svuu)) / denominator;
            double vc = (suu * (svvv + svuu) - suv * (suuu + suvv)) / denominator;

            double centerX = xAvg + uc;
            double centerY = yAvg + vc;

            return points
                .Select(p => Math.Sqrt(Math.Pow(p.X - centerX, 2) + Math.Pow(p.Y - centerY, 2)))
                .Average();
        }
    }
}
