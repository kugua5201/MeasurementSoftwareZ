namespace MeasurementSoftware.Models
{
    /// <summary>
    /// 日志条目
    /// </summary>
    public class LogEntry
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
    }
}
