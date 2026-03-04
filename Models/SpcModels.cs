namespace MeasurementSoftware.Models
{
    /// <summary>
    /// SPC分析结果
    /// </summary>
    public class SpcResult
    {
        /// <summary>
        /// 通道名称
        /// </summary>
        public string ChannelName { get; set; } = string.Empty;

        /// <summary>
        /// 样本均值
        /// </summary>
        public double Mean { get; set; }

        /// <summary>
        /// 标准差
        /// </summary>
        public double StdDev { get; set; }

        /// <summary>
        /// 过程能力指数 Cp
        /// </summary>
        public double Cp { get; set; }

        /// <summary>
        /// 过程能力指数 Cpk
        /// </summary>
        public double Cpk { get; set; }

        /// <summary>
        /// 合格率(%)
        /// </summary>
        public double YieldRate { get; set; }

        /// <summary>
        /// 样本数量
        /// </summary>
        public int SampleCount { get; set; }

        /// <summary>
        /// 规格上限 USL
        /// </summary>
        public double USL { get; set; }

        /// <summary>
        /// 规格下限 LSL
        /// </summary>
        public double LSL { get; set; }

        /// <summary>
        /// 标准值（规格中心）
        /// </summary>
        public double Nominal { get; set; }

        /// <summary>
        /// 最大值
        /// </summary>
        public double Max { get; set; }

        /// <summary>
        /// 最小值
        /// </summary>
        public double Min { get; set; }

        /// <summary>
        /// 极差 R
        /// </summary>
        public double Range { get; set; }

        /// <summary>
        /// Cpk等级评定
        /// </summary>
        public string CpkLevel => Cpk switch
        {
            >= 1.67 => "A (优秀)",
            >= 1.33 => "B (良好)",
            >= 1.0 => "C (一般)",
            >= 0.67 => "D (较差)",
            _ => "E (不合格)"
        };
    }

    /// <summary>
    /// Xbar-R 控制图数据点
    /// </summary>
    public class ControlChartPoint
    {
        public int SubgroupIndex { get; set; }
        public double XbarValue { get; set; }
        public double RangeValue { get; set; }
    }

    /// <summary>
    /// 控制图界限
    /// </summary>
    public class ControlLimits
    {
        /// <summary>
        /// Xbar 中心线
        /// </summary>
        public double XbarCL { get; set; }

        /// <summary>
        /// Xbar 上控制限
        /// </summary>
        public double XbarUCL { get; set; }

        /// <summary>
        /// Xbar 下控制限
        /// </summary>
        public double XbarLCL { get; set; }

        /// <summary>
        /// R 中心线
        /// </summary>
        public double RCL { get; set; }

        /// <summary>
        /// R 上控制限
        /// </summary>
        public double RUCL { get; set; }

        /// <summary>
        /// R 下控制限
        /// </summary>
        public double RLCL { get; set; }
    }

    /// <summary>
    /// Xbar-R控制图的完整数据
    /// </summary>
    public class XbarRChartData
    {
        public List<ControlChartPoint> Points { get; set; } = [];
        public ControlLimits Limits { get; set; } = new();
    }
}
