using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services
{
    /// <summary>
    /// SPC统计过程控制服务接口
    /// </summary>
    public interface ISpcService
    {
        /// <summary>
        /// 计算通道的SPC指标
        /// </summary>
        SpcResult CalculateSpc(string channelName, List<double> data, double nominal, double upperTolerance, double lowerTolerance);

        /// <summary>
        /// 生成 Xbar-R 控制图数据
        /// </summary>
        XbarRChartData GenerateXbarRChart(List<double> data, int subgroupSize = 5);

        /// <summary>
        /// 生成正态分布直方图数据（返回区间中心值和频次）
        /// </summary>
        (double[] BinCenters, int[] Frequencies) GenerateHistogram(List<double> data, int binCount = 20);
    }
}
