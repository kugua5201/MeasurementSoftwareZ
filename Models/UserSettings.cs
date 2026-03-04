namespace MeasurementSoftware.Models
{
    /// <summary>
    /// 用户设置（保存应用级别的状态）
    /// </summary>
    public class UserSettings
    {
        /// <summary>
        /// 上次打开的配方文件路径
        /// </summary>
        public string LastRecipePath { get; set; } = string.Empty;

        /// <summary>
        /// 上次更新时间
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}
