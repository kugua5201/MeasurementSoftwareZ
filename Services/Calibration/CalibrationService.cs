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

        public CalibrationService(ILog log)
        {
            _log = log;
        }

        /// <summary>
        /// 多点校准（最小二乘法线性拟合）
        /// </summary>
        public async Task<(bool Success, double CoefficientA, double CoefficientB)> CalibrateLeastSquaresAsync(
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
                channel.CalibrationMode = CalibrationMode.LeastSquares;
                channel.CalibrationCoefficientA = coefficientA;
                channel.CalibrationCoefficientB = coefficientB;
                channel.LastCalibrationTime = DateTime.Now;
                channel.LeastSquaresCalibration.CoefficientA = coefficientA;
                channel.LeastSquaresCalibration.CoefficientB = coefficientB;
                channel.LeastSquaresCalibration.LastCalibrationTime = channel.LastCalibrationTime;
                channel.LeastSquaresCalibration.Points = [.. calibrationPoints.Select((p, index) => new LeastSquaresCalibrationPoint
                {
                    Index = index + 1,
                    StandardValue = p.StandardValue,
                    MeasuredValue = p.MeasuredValue
                })];

                // 保存校准记录
                var record = new CalibrationRecord
                {
                    ChannelId = $"{channel.ChannelNumber}",
                    CalibrationTime = DateTime.Now,
                    Mode = CalibrationMode.LeastSquares,
                    MethodName = "最小二乘法",
                    CoefficientA = coefficientA,
                    CoefficientB = coefficientB,
                    CalibrationPoints = [.. calibrationPoints.Select(p => new CalibrationRecordPoint
                    {
                        StandardValue = p.StandardValue,
                        MeasuredValue = p.MeasuredValue
                    })]
                };
                channel.CalibrationHistory.Add(record);

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
        /// 线性回归校准
        /// </summary>
        public async Task<(bool Success, double CoefficientA, double CoefficientB)> CalibrateLinearRegressionAsync(
            MeasurementChannel channel,
            List<(double StandardValue, double MeasuredValue)> calibrationPoints)
        {
            try
            {
                if (calibrationPoints == null || calibrationPoints.Count < 2)
                {
                    _log.Error("线性回归校准点数量不足，至少需要2个点");
                    return (false, 1.0, 0.0);
                }

                var measuredValues = calibrationPoints.Select(p => p.MeasuredValue).ToList();
                var standardValues = calibrationPoints.Select(p => p.StandardValue).ToList();
                var meanX = measuredValues.Average();
                var meanY = standardValues.Average();

                double covariance = 0;
                double variance = 0;
                for (int i = 0; i < calibrationPoints.Count; i++)
                {
                    var dx = measuredValues[i] - meanX;
                    covariance += dx * (standardValues[i] - meanY);
                    variance += dx * dx;
                }

                if (Math.Abs(variance) < 1e-10)
                {
                    _log.Error("线性回归校准失败：测量值方差为0");
                    return (false, 1.0, 0.0);
                }

                var coefficientA = covariance / variance;
                var coefficientB = meanY - coefficientA * meanX;

                channel.CalibrationMode = CalibrationMode.LinearRegression;
                channel.CalibrationCoefficientA = coefficientA;
                channel.CalibrationCoefficientB = coefficientB;
                channel.LastCalibrationTime = DateTime.Now;
                channel.LinearRegressionCalibration.CoefficientA = coefficientA;
                channel.LinearRegressionCalibration.CoefficientB = coefficientB;
                channel.LinearRegressionCalibration.LastCalibrationTime = channel.LastCalibrationTime;
                channel.LinearRegressionCalibration.Points = [.. calibrationPoints.Select((p, index) => new LinearRegressionCalibrationPoint
                {
                    Index = index + 1,
                    StandardValue = p.StandardValue,
                    MeasuredValue = p.MeasuredValue
                })];

                var record = new CalibrationRecord
                {
                    ChannelId = $"{channel.ChannelNumber}",
                    CalibrationTime = DateTime.Now,
                    Mode = CalibrationMode.LinearRegression,
                    MethodName = "线性回归",
                    CoefficientA = coefficientA,
                    CoefficientB = coefficientB,
                    CalibrationPoints = [.. calibrationPoints.Select(p => new CalibrationRecordPoint
                    {
                        StandardValue = p.StandardValue,
                        MeasuredValue = p.MeasuredValue
                    })]
                };
                channel.CalibrationHistory.Add(record);

                _log.Info($"通道 {channel.ChannelName} 线性回归校准成功: A={coefficientA:F6}, B={coefficientB:F6}");
                return await Task.FromResult((true, coefficientA, coefficientB));
            }
            catch (Exception ex)
            {
                _log.Error($"线性回归校准失败: {ex.Message}");
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

                channel.CalibrationMode = CalibrationMode.SinglePoint;
                channel.CalibrationCoefficientA = 1.0;
                channel.CalibrationCoefficientB = coefficientB;
                channel.LastCalibrationTime = DateTime.Now;
                channel.SinglePointCalibration.StandardValue = standardValue;
                channel.SinglePointCalibration.MeasuredValue = measuredValue;
                channel.SinglePointCalibration.CoefficientA = 1.0;
                channel.SinglePointCalibration.CoefficientB = coefficientB;
                channel.SinglePointCalibration.LastCalibrationTime = channel.LastCalibrationTime;

                var record = new CalibrationRecord
                {
                    ChannelId = $"{channel.ChannelNumber}",
                    CalibrationTime = DateTime.Now,
                    Mode = CalibrationMode.SinglePoint,
                    MethodName = "单点校准",
                    CoefficientA = 1.0,
                    CoefficientB = coefficientB,
                    CalibrationPoints = [new CalibrationRecordPoint
                    {
                        StandardValue = standardValue,
                        MeasuredValue = measuredValue
                    }]
                };
                channel.CalibrationHistory.Add(record);

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

        public async Task<List<CalibrationRecord>> GetCalibrationHistoryAsync(MeasurementChannel channel)
        {
            return await Task.FromResult(channel.CalibrationHistory.OrderByDescending(r => r.CalibrationTime).ToList());
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
