using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Logs;

namespace MeasurementSoftware.Services
{
    /// <summary>
    /// SPC统计过程控制服务实现
    /// </summary>
    public class SpcService : ISpcService
    {
        private readonly ILog _log;

        // Xbar-R控制图常数表 (subgroup size n -> A2, D3, D4)
        private static readonly Dictionary<int, (double A2, double D3, double D4)> ControlChartConstants = new()
        {
            { 2, (1.880, 0.000, 3.267) },
            { 3, (1.023, 0.000, 2.574) },
            { 4, (0.729, 0.000, 2.282) },
            { 5, (0.577, 0.000, 2.114) },
            { 6, (0.483, 0.000, 2.004) },
            { 7, (0.419, 0.076, 1.924) },
            { 8, (0.373, 0.136, 1.864) },
            { 9, (0.337, 0.184, 1.816) },
            { 10, (0.308, 0.223, 1.777) }
        };

        // d2 常数（用于从 Rbar 估计 sigma）
        private static readonly Dictionary<int, double> D2Constants = new()
        {
            { 2, 1.128 }, { 3, 1.693 }, { 4, 2.059 }, { 5, 2.326 },
            { 6, 2.534 }, { 7, 2.704 }, { 8, 2.847 }, { 9, 2.970 }, { 10, 3.078 }
        };

        public SpcService(ILog log)
        {
            _log = log;
        }

        public SpcResult CalculateSpc(string channelName, List<double> data, double nominal, double upperTolerance, double lowerTolerance)
        {
            if (data.Count == 0)
            {
                return new SpcResult { ChannelName = channelName, SampleCount = 0 };
            }

            var usl = nominal + upperTolerance;
            var lsl = nominal - lowerTolerance;

            var mean = data.Average();
            var stdDev = CalculateStdDev(data, mean);
            var max = data.Max();
            var min = data.Min();

            double cp = 0, cpk = 0;
            if (stdDev > 1e-10)
            {
                cp = (usl - lsl) / (6 * stdDev);
                var cpu = (usl - mean) / (3 * stdDev);
                var cpl = (mean - lsl) / (3 * stdDev);
                cpk = Math.Min(cpu, cpl);
            }

            var passCount = data.Count(v => v >= lsl && v <= usl);
            var yieldRate = (double)passCount / data.Count * 100;

            var result = new SpcResult
            {
                ChannelName = channelName,
                Mean = mean,
                StdDev = stdDev,
                Cp = cp,
                Cpk = cpk,
                YieldRate = yieldRate,
                SampleCount = data.Count,
                USL = usl,
                LSL = lsl,
                Nominal = nominal,
                Max = max,
                Min = min,
                Range = max - min
            };

            _log.Info($"SPC计算完成: {channelName}, Cp={cp:F3}, Cpk={cpk:F3}, 合格率={yieldRate:F1}%");
            return result;
        }

        public XbarRChartData GenerateXbarRChart(List<double> data, int subgroupSize = 5)
        {
            var chartData = new XbarRChartData();

            if (data.Count < subgroupSize * 2)
            {
                _log.Warn($"数据量不足，至少需要 {subgroupSize * 2} 个样本才能生成Xbar-R控制图");
                return chartData;
            }

            var subgroupCount = data.Count / subgroupSize;
            var points = new List<ControlChartPoint>();

            for (int i = 0; i < subgroupCount; i++)
            {
                var subgroup = data.Skip(i * subgroupSize).Take(subgroupSize).ToList();
                points.Add(new ControlChartPoint
                {
                    SubgroupIndex = i + 1,
                    XbarValue = subgroup.Average(),
                    RangeValue = subgroup.Max() - subgroup.Min()
                });
            }

            var xbarBar = points.Average(p => p.XbarValue);
            var rBar = points.Average(p => p.RangeValue);

            if (!ControlChartConstants.TryGetValue(subgroupSize, out var constants))
                constants = ControlChartConstants[5];

            chartData.Points = points;
            chartData.Limits = new ControlLimits
            {
                XbarCL = xbarBar,
                XbarUCL = xbarBar + constants.A2 * rBar,
                XbarLCL = xbarBar - constants.A2 * rBar,
                RCL = rBar,
                RUCL = constants.D4 * rBar,
                RLCL = constants.D3 * rBar
            };

            return chartData;
        }

        public (double[] BinCenters, int[] Frequencies) GenerateHistogram(List<double> data, int binCount = 20)
        {
            if (data.Count == 0)
                return ([], []);

            var min = data.Min();
            var max = data.Max();
            var range = max - min;

            if (range < 1e-10)
            {
                return ([min], [data.Count]);
            }

            var binWidth = range / binCount;
            var binCenters = new double[binCount];
            var frequencies = new int[binCount];

            for (int i = 0; i < binCount; i++)
            {
                binCenters[i] = min + binWidth * (i + 0.5);
            }

            foreach (var value in data)
            {
                var binIndex = (int)((value - min) / binWidth);
                if (binIndex >= binCount) binIndex = binCount - 1;
                if (binIndex < 0) binIndex = 0;
                frequencies[binIndex]++;
            }

            return (binCenters, frequencies);
        }

        private static double CalculateStdDev(List<double> data, double mean)
        {
            if (data.Count <= 1) return 0;
            var sumSquares = data.Sum(v => (v - mean) * (v - mean));
            return Math.Sqrt(sumSquares / (data.Count - 1));
        }
    }
}
