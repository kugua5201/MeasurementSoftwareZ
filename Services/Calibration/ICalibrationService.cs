using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services
{
    /// <summary>
    /// 校准服务接口
    /// </summary>
    public interface ICalibrationService
    {
        /// <summary>
        /// 执行最小二乘法校准
        /// </summary>
        Task<(bool Success, double CoefficientA, double CoefficientB)> CalibrateLeastSquaresAsync(
            MeasurementChannel channel,
            List<(double StandardValue, double MeasuredValue)> calibrationPoints);

        /// <summary>
        /// 执行线性回归校准
        /// </summary>
        Task<(bool Success, double CoefficientA, double CoefficientB)> CalibrateLinearRegressionAsync(
            MeasurementChannel channel,
            List<(double StandardValue, double MeasuredValue)> calibrationPoints);

        /// <summary>
        /// 执行单点校准（简单偏移）
        /// </summary>
        Task<(bool Success, double CoefficientB)> CalibrateSinglePointAsync(
            MeasurementChannel channel, 
            double standardValue, 
            double measuredValue);

        /// <summary>
        /// 保存校准结果
        /// </summary>
        Task<bool> SaveCalibrationAsync(MeasurementChannel channel);

        /// <summary>
        /// 加载校准历史
        /// </summary>
        Task<List<CalibrationRecord>> GetCalibrationHistoryAsync(MeasurementChannel channel);

        /// <summary>
        /// 检查校准有效期
        /// </summary>
        bool CheckCalibrationValidity(MeasurementChannel channel);

        /// <summary>
        /// 应用校准到测量值
        /// </summary>
        double ApplyCalibration(MeasurementChannel channel, double rawValue);
    }
}
