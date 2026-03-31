namespace MeasurementSoftware.Services.Devices
{
    /// <summary>
    /// 支持缓存块读取能力的 PLC 运行时接口。
    /// 仅由具备缓存机制的协议实现，例如西门子运行时。
    /// </summary>
    public interface ICachePlcDeviceRuntime : IPlcDeviceRuntime
    {
        /// <summary>
        /// 启动缓存读取。
        /// </summary>
        void StartCacheReading();

        /// <summary>
        /// 停止缓存读取。
        /// </summary>
        void StopCacheReading();

        /// <summary>
        /// 获取缓存字段的当前值。
        /// </summary>
        /// <param name="cacheFieldId">缓存字段键。</param>
        /// <returns>成功时返回数值；否则返回 null。</returns>
        double? GetCacheFieldValue(string cacheFieldId);

        /// <summary>
        /// 取出并清空指定缓存字段的待处理历史值。
        /// </summary>
        /// <param name="cacheFieldId">缓存字段键。</param>
        /// <returns>本次尚未消费的历史值列表。</returns>
        IReadOnlyList<double> TakeCacheFieldValues(string cacheFieldId);
    }
}
