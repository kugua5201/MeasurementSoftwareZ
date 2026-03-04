using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Logs;

namespace MeasurementSoftware.Services
{
    /// <summary>
    /// 校准服务实现（线性匹配）
    /// </summary>
    public class CalibrationService : ICalibrationService
    {
        private readonly ILog _log;
        private readonly List<CalibrationRecord> _calibrationHistory = new();

        public CalibrationService(ILog log)
        {
            _log = log;
        }

        /// <summary>
        /// 多点校准（最小二乘法线性拟合）
        /// </summary>
        public async Task<(bool Success, double CoefficientA, double CoefficientB)> CalibrateChannelAsync(
            MeasurementChannel channel,
            List<(double StandardValue, double MeasuredValue)> calibrationPoints)
        {
            try
            {
                if (calibrationPoints == null || calibrationPoints.Count < 2)
                {
                    _log.Error("校准点数量不足，至少需要2个点");
                    return (false, 1.0, 0.0);
                }

                // 使用最小二乘法计算线性系数
                // y = Ax + B，其中 x 是测量值，y 是标准值
                var n = calibrationPoints.Count;
                double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;

                foreach (var point in calibrationPoints)
                {
                    sumX += point.MeasuredValue;
                    sumY += point.StandardValue;
                    sumXY += point.MeasuredValue * point.StandardValue;
                    sumX2 += point.MeasuredValue * point.MeasuredValue;
                }

                // 计算斜率 A 和截距 B
                var denominator = n * sumX2 - sumX * sumX;
                if (Math.Abs(denominator) < 1e-10)
                {
                    _log.Error("校准计算失败：数据共线");
                    return (false, 1.0, 0.0);
                }

                var coefficientA = (n * sumXY - sumX * sumY) / denominator;
                var coefficientB = (sumY - coefficientA * sumX) / n;

                // 更新通道校准参数
                channel.CalibrationCoefficientA = coefficientA;
                channel.CalibrationCoefficientB = coefficientB;
                channel.LastCalibrationTime = DateTime.Now;

                // 保存校准记录
                var record = new CalibrationRecord
                {
                    ChannelId = $"{channel.ChannelNumber}",
                    CalibrationTime = DateTime.Now,
                    CoefficientA = coefficientA,
                    CoefficientB = coefficientB,
                    CalibrationPoints = calibrationPoints
                };
                _calibrationHistory.Add(record);

                _log.Info($"通道 {channel.ChannelName} 校准成功: A={coefficientA:F6}, B={coefficientB:F6}");
                return await Task.FromResult((true, coefficientA, coefficientB));
            }
            catch (Exception ex)
            {
                _log.Error($"校准失败: {ex.Message}");
                return (false, 1.0, 0.0);
            }
        }

        /// <summary>
        /// 单点校准（简单偏移）
        /// </summary>
        public async Task<(bool Success, double CoefficientB)> CalibrateSinglePointAsync(
            MeasurementChannel channel,
            double standardValue,
            double measuredValue)
        {
            try
            {
                // 单点校准：y = x + B，其中 B = 标准值 - 测量值
                var coefficientB = standardValue - measuredValue;

                channel.CalibrationCoefficientA = 1.0;
                channel.CalibrationCoefficientB = coefficientB;
                channel.LastCalibrationTime = DateTime.Now;

                var record = new CalibrationRecord
                {
                    ChannelId = $"{channel.ChannelNumber}",
                    CalibrationTime = DateTime.Now,
                    CoefficientA = 1.0,
                    CoefficientB = coefficientB,
                    CalibrationPoints = new List<(double, double)> { (standardValue, measuredValue) }
                };
                _calibrationHistory.Add(record);

                _log.Info($"通道 {channel.ChannelName} 单点校准成功: B={coefficientB:F6}");
                return await Task.FromResult((true, coefficientB));
            }
            catch (Exception ex)
            {
                _log.Error($"单点校准失败: {ex.Message}");
                return (false, 0.0);
            }
        }

        public async Task<bool> SaveCalibrationAsync(MeasurementChannel channel)
        {
            // 校准数据已经保存在通道对象中
            // 这里可以扩展为保存到数据库
            _log.Info($"通道 {channel.ChannelName} 校准数据已保存");
            return await Task.FromResult(true);
        }

        public async Task<List<CalibrationRecord>> GetCalibrationHistoryAsync(string channelId)
        {
            return await Task.FromResult(
                _calibrationHistory.Where(r => r.ChannelId == channelId).ToList()
            );
        }

        public bool CheckCalibrationValidity(MeasurementChannel channel)
        {
            if (!channel.RequiresCalibration)
                return true;

            if (!channel.LastCalibrationTime.HasValue)
                return false;

            var expiryDate = channel.LastCalibrationTime.Value.AddDays(channel.CalibrationValidityDays);
            var isValid = DateTime.Now <= expiryDate;

            if (!isValid)
            {
                _log.Warn($"通道 {channel.ChannelName} 校准已过期");
            }

            return isValid;
        }

        public double ApplyCalibration(MeasurementChannel channel, double rawValue)
        {
            if (!channel.RequiresCalibration)
                return rawValue;

            // 应用线性校准公式：y = Ax + B
            return channel.CalibrationCoefficientA * rawValue + channel.CalibrationCoefficientB;
        }
    }
}
