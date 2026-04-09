using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services.Devices
{
    /// <summary>
    /// PLC 点位值更新事件参数。
    /// </summary>
    public sealed class PlcDataPointsUpdatedEventArgs : EventArgs
    {
        public PlcDataPointsUpdatedEventArgs(PlcDevice device, IReadOnlyList<DataPoint> dataPoints, DateTime updateTime)
        {
            Device = device ?? throw new ArgumentNullException(nameof(device));
            DataPoints = dataPoints ?? throw new ArgumentNullException(nameof(dataPoints));
            UpdateTime = updateTime;
        }

        /// <summary>
        /// 本次产生更新的设备。
        /// </summary>
        public PlcDevice Device { get; }

        /// <summary>
        /// 本次已更新完成的点位集合。
        /// </summary>
        public IReadOnlyList<DataPoint> DataPoints { get; }

        /// <summary>
        /// 本次更新对应的时间。
        /// </summary>
        public DateTime UpdateTime { get; }
    }

    /// <summary>
    /// PLC 缓存字段单项更新数据。
    /// </summary>
    public sealed class PlcCacheFieldUpdateItem
    {
        /// <summary>
        /// 缓存字段键。
        /// </summary>
        public string CacheFieldKey { get; init; } = string.Empty;

        /// <summary>
        /// 当前字段最新解析值。
        /// </summary>
        public object? LatestValue { get; init; }

        /// <summary>
        /// 本次推送的数值历史片段。
        /// 适用于按缓存批次更新通道历史值。
        /// </summary>
        public IReadOnlyList<double> NumericValues { get; init; } = [];

        /// <summary>
        /// 本次字段更新是否成功。
        /// </summary>
        public bool IsSuccess { get; init; }

        /// <summary>
        /// 本次字段更新说明或错误信息。
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// 本次字段更新时间。
        /// </summary>
        public DateTime UpdateTime { get; init; }
    }

    /// <summary>
    /// PLC 缓存字段更新事件参数。
    /// </summary>
    public sealed class PlcCacheFieldsUpdatedEventArgs : EventArgs
    {
        public PlcCacheFieldsUpdatedEventArgs(PlcDevice device, IReadOnlyList<PlcCacheFieldUpdateItem> updates, int cacheIndex, DateTime updateTime)
        {
            Device = device ?? throw new ArgumentNullException(nameof(device));
            Updates = updates ?? throw new ArgumentNullException(nameof(updates));
            CacheIndex = cacheIndex;
            UpdateTime = updateTime;
        }

        /// <summary>
        /// 本次产生缓存更新的设备。
        /// </summary>
        public PlcDevice Device { get; }

        /// <summary>
        /// 本次解析完成的缓存字段更新集合。
        /// </summary>
        public IReadOnlyList<PlcCacheFieldUpdateItem> Updates { get; }

        /// <summary>
        /// 产生更新的缓存索引。
        /// </summary>
        public int CacheIndex { get; }

        /// <summary>
        /// 本次缓存更新时间。
        /// </summary>
        public DateTime UpdateTime { get; }
    }

    /// <summary>
    /// PLC 设备连接状态变化事件参数。
    /// </summary>
    public sealed class PlcDeviceConnectionChangedEventArgs : EventArgs
    {
        public PlcDeviceConnectionChangedEventArgs(PlcDevice device, bool isConnected, DateTime changeTime)
        {
            Device = device ?? throw new ArgumentNullException(nameof(device));
            IsConnected = isConnected;
            ChangeTime = changeTime;
        }

        /// <summary>
        /// 状态变化对应的设备。
        /// </summary>
        public PlcDevice Device { get; }

        /// <summary>
        /// 当前是否已连接。
        /// </summary>
        public bool IsConnected { get; }

        /// <summary>
        /// 状态变化时间。
        /// </summary>
        public DateTime ChangeTime { get; }
    }
}
