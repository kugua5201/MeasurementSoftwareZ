using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services
{
    /// <summary>
    /// 测量服务接口 — 封装PLC数据采集、通道类型计算、测量流程控制
    /// </summary>
    public interface IMeasurementService
    {
        /// <summary>
        /// 是否正在采集
        /// </summary>
        bool IsAcquiring { get; }

        /// <summary>
        /// 单次采集完成事件（每轮采集结束后触发）
        /// </summary>
        event EventHandler<MeasurementCompletedEventArgs>? MeasurementCompleted;

        /// <summary>
        /// 实时数据更新事件（连续采集时每次读取后触发）
        /// </summary>
        event EventHandler<RealTimeDataEventArgs>? RealTimeDataUpdated;

        /// <summary>
        /// 启动测量（根据配方进行一次完整测量）
        /// </summary>
        Task<MeasurementSessionResult> StartMeasurementAsync(MeasurementRecipe recipe, int stepNumber = 0);

        /// <summary>
        /// 停止采集
        /// </summary>
        void StopMeasurement();

        /// <summary>
        /// 单次读取指定通道的数据
        /// </summary>
        Task<double?> ReadChannelValueAsync(MeasurementChannel channel);

        /// <summary>
        /// 根据通道类型计算统计值
        /// </summary>
        ChannelStatistics CalculateStatistics(MeasurementChannel channel);
    }

    /// <summary>
    /// 测量完成事件参数
    /// </summary>
    public class MeasurementCompletedEventArgs : EventArgs
    {
        public MeasurementResult OverallResult { get; set; }
        public List<ChannelMeasurementData> ChannelResults { get; set; } = [];
    }

    /// <summary>
    /// 实时数据更新事件参数
    /// </summary>
    public class RealTimeDataEventArgs : EventArgs
    {
        public int ChannelNumber { get; set; }
        public double Value { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 单次测量会话的结果
    /// </summary>
    public class MeasurementSessionResult
    {
        public bool Success { get; set; }
        public MeasurementResult OverallResult { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<ChannelMeasurementData> ChannelResults { get; set; } = [];
        public DateTime MeasurementTime { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 通道统计数据（用于连续采集类通道）
    /// </summary>
    public class ChannelStatistics
    {
        public double Max { get; set; }
        public double Min { get; set; }
        public double Average { get; set; }
        /// <summary>
        /// 跳动值 = Max - Min
        /// </summary>
        public double Runout { get; set; }
        public int SampleCount { get; set; }
    }
}
