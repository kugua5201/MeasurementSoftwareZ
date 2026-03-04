using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services
{
    /// <summary>
    /// 校准服务接口
    /// </summary>
    public interface ICalibrationService
    {
        /// <summary>
        /// 执行通道校准（多点校准，自动计算线性系数）
        /// </summary>
        Task<(bool Success, double CoefficientA, double CoefficientB)> CalibrateChannelAsync(
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
        Task<List<CalibrationRecord>> GetCalibrationHistoryAsync(string channelId);

        /// <summary>
        /// 检查校准有效期
        /// </summary>
        bool CheckCalibrationValidity(MeasurementChannel channel);

        /// <summary>
        /// 应用校准到测量值
        /// </summary>
        double ApplyCalibration(MeasurementChannel channel, double rawValue);
    }

    /// <summary>
    /// 校准记录
    /// </summary>
    public class CalibrationRecord
    {
        public string RecordId { get; set; } = Guid.NewGuid().ToString();
        public string ChannelId { get; set; } = string.Empty;
        public DateTime CalibrationTime { get; set; } = DateTime.Now;
        public double CoefficientA { get; set; }
        public double CoefficientB { get; set; }
        public string OperatorName { get; set; } = string.Empty;
        public List<(double StandardValue, double MeasuredValue)> CalibrationPoints { get; set; } = new();
    }
}
