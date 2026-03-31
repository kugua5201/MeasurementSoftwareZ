using MeasurementSoftware.Models;
using MultiProtocol.Model;
using MultiProtocol.Services.IIndustrialProtocol;
using System.Collections.Concurrent;
using DriveType = MultiProtocol.Model.DriveType;

namespace MeasurementSoftware.Services.Devices.Siemens
{
    /// <summary>
    /// 西门子设备运行时抽象基类。
    /// 在通用点位读写能力之上，额外提供缓存读取与解析能力。
    /// </summary>
    public abstract class SiemensPlcDeviceRuntimeBase : PlcDeviceRuntimeBase, ICachePlcDeviceRuntime
    {
        private readonly ConcurrentDictionary<string, object?> _cacheFieldValues = new();
        private readonly ConcurrentDictionary<string, ConcurrentQueue<double>> _cacheFieldHistoryValues = new();


        protected SiemensPlcDeviceRuntimeBase(PlcDevice device) : base(device)
        {
        }

        /// <inheritdoc />
        public void StartCacheReading()
        {
            if (Protocol == null || !Device.SiemensReadCache.IsEnabled)
            {
                return;
            }

            var cache = Device.SiemensReadCache;
            foreach (var field in cache.ExpandedFieldDefinitions)
            {
                field.ParsedValue = null;
                field.Description = string.Empty;
                _cacheFieldValues.TryRemove(field.CacheFieldKey, out _);
                _cacheFieldHistoryValues.TryRemove(field.CacheFieldKey, out _);
            }

            Protocol.StartCacheReading(
                $"{cache.Cache1.DbBlock}", cache.Cache1.LengthAddress, cache.Cache1.ReadableFlagAddress, cache.Cache1.Length,
                $"{cache.Cache2.DbBlock}", cache.Cache2.LengthAddress, cache.Cache2.ReadableFlagAddress, cache.Cache2.Length);
            Device.IsCacheReading = true;
        }

        /// <inheritdoc />
        public void StopCacheReading()
        {
            Protocol?.StopCacheReading();
            Device.IsCacheReading = false;
        }

        /// <inheritdoc />
        public override async Task DisconnectAsync()
        {
            StopCacheReading();
            await base.DisconnectAsync();
        }

        /// <inheritdoc />
        public override async Task DestroyAsync()
        {
            StopCacheReading();
            await base.DestroyAsync();
        }

        /// <inheritdoc />
        public double? GetCacheFieldValue(string cacheFieldId)
        {
            if (_cacheFieldValues.TryGetValue(cacheFieldId, out var value) && value != null)
            {
                try
                {
                    return Convert.ToDouble(value);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        /// <inheritdoc />
        public IReadOnlyList<double> TakeCacheFieldValues(string cacheFieldId)
        {
            if (!_cacheFieldHistoryValues.TryGetValue(cacheFieldId, out var queue))
            {
                return [];
            }

            List<double> values = [];
            while (queue.TryDequeue(out var value))
            {
                values.Add(value);
            }

            return values;
        }

        /// <inheritdoc />
        public override void SetPollingEnabled(bool enabled)
        {
            if (!enabled)
            {
                StopCacheReading();
            }

            base.SetPollingEnabled(enabled);
        }

        protected override void SubscribeProtocolEvents(IIndustrialProtocol protocol)
        {
            base.SubscribeProtocolEvents(protocol);
            protocol.OnCacheDataRead += Protocol_OnCacheDataRead;
        }

        protected override void UnsubscribeProtocolEvents(IIndustrialProtocol protocol)
        {
            protocol.OnCacheDataRead -= Protocol_OnCacheDataRead;
            base.UnsubscribeProtocolEvents(protocol);
        }

        protected override void OnProtocolConnectChanged(bool connected)
        {
            base.OnProtocolConnectChanged(connected);
            if (connected)
            {
                if (Device.SiemensReadCache.IsEnabled && Device.SiemensReadCache.IsStructureValid)
                {
                    StartCacheReading();
                }
            }
            else
            {
                StopCacheReading();
            }
        }

        protected override DriveType GetDriveType()
        {
            return GetSiemensDriveType();
        }

        /// <summary>
        /// 返回具体西门子驱动类型。
        /// </summary>
        protected abstract DriveType GetSiemensDriveType();

        private void Protocol_OnCacheDataRead(object? sender, CacheDataEventArgs data)
        {
            if (!Device.SiemensReadCache.IsEnabled || !Device.SiemensReadCache.IsStructureValid)
            {
                return;
            }
            //判断是否在采集数据了，如果没有就不进行处理数据
            
            if (!data.IsSuccess)
            {
                SetCacheFieldDescriptions(data.CacheIndex, string.IsNullOrWhiteSpace(data.Message) ? "缓存读取失败" : data.Message);
                return;
            }

            if (data.Data == null || data.Data.Length == 0)
            {
                SetCacheFieldDescriptions(data.CacheIndex, string.IsNullOrWhiteSpace(data.Message) ? "未收到缓存数据" : data.Message);
                return;
            }

            var cache = Device.SiemensReadCache;
            var now = DateTime.Now;

            if (cache.GroupSize <= 0)
            {
                SetCacheFieldDescriptions(data.CacheIndex, "缓存结构大小无效，请重新验证结构定义");
                return;
            }

            int recordCount = data.Data.Length / cache.GroupSize;
            int remainder = data.Data.Length % cache.GroupSize;
            if (recordCount <= 0)
            {
                SetCacheFieldDescriptions(data.CacheIndex, $"缓存长度不足，当前{data.Data.Length}字节，小于单条结构{cache.GroupSize}字节");
                return;
            }

            foreach (var expandedField in cache.ExpandedFieldDefinitions)
            {
                object? latestValue = null;
                string description = remainder > 0 ? $"本次解析 {recordCount} 条，尾部剩余 {remainder} 字节" : string.Empty;
                bool hasError = false;

                for (int recordIndex = 0; recordIndex < recordCount; recordIndex++)
                {
                    int offset = recordIndex * cache.GroupSize + expandedField.Offset;
                    int size = SiemensReadCacheConfig.GetFieldTypeSize(expandedField.DataType);
                    if (offset + size > data.Data.Length)
                    {
                        description = $"第 {recordIndex + 1} 条数据长度不足";
                        hasError = true;
                        break;
                    }

                    try
                    {
                        byte[] segment = new byte[size];
                        Array.Copy(data.Data, offset, segment, 0, size);
                        var value = ParseByteSegment(segment, expandedField.DataType, expandedField.ByteOrder);
                        latestValue = value;

                        if (TryConvertToDouble(value, out var numericValue))
                        {
                            var queue = _cacheFieldHistoryValues.GetOrAdd(expandedField.CacheFieldKey, _ => new ConcurrentQueue<double>());
                            queue.Enqueue(numericValue);
                            var maxPending = Math.Clamp(Device.SiemensReadCache.MaxCacheCount, 1, 9999999);
                            while (queue.Count > maxPending)
                            {
                                queue.TryDequeue(out _);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        description = $"第 {recordIndex + 1} 条解析失败: {ex.Message}";
                        hasError = true;
                        break;
                    }
                }

                expandedField.ParsedValue = latestValue;
                expandedField.Description = description;
                expandedField.LastUpdateTime = now;

                if (latestValue != null)
                {
                    _cacheFieldValues[expandedField.CacheFieldKey] = latestValue;
                    UpdateCacheGeneratedPoint(expandedField.CacheFieldKey, latestValue, now, hasError ? description : null);
                }
                else
                {
                    _cacheFieldValues.TryRemove(expandedField.CacheFieldKey, out _);
                    UpdateCacheGeneratedPoint(expandedField.CacheFieldKey, null, now, hasError ? description : "未解析到有效数据");
                }
            }
        }

        private void SetCacheFieldDescriptions(int cacheIndex, string message)
        {
            var now = DateTime.Now;
            foreach (var field in Device.SiemensReadCache.ExpandedFieldDefinitions)
            {
                field.Description = message;
                field.ParsedValue = null;
                field.LastUpdateTime = now;
                _cacheFieldValues.TryRemove(field.CacheFieldKey, out _);
                UpdateCacheGeneratedPoint(field.CacheFieldKey, null, now, message);
            }
        }

        private void UpdateCacheGeneratedPoint(string cacheFieldKey, object? value, DateTime updateTime, string? errorMessage)
        {
            var dataPoint = Device.DataPoints.FirstOrDefault(dp => dp.IsCacheGenerated && dp.CacheFieldKey == cacheFieldKey);
            if (dataPoint == null)
            {
                return;
            }

            dataPoint.LastUpdateTime = updateTime;
            if (value != null)
            {
                dataPoint.CurrentValue = value;
                dataPoint.IsSuccess = true;
                dataPoint.ErrorMessage = errorMessage;
            }
            else
            {
                dataPoint.IsSuccess = false;
                dataPoint.ErrorMessage = errorMessage;
            }
        }

        private static bool TryConvertToDouble(object? value, out double result)
        {
            try
            {
                if (value != null)
                {
                    result = Convert.ToDouble(value);
                    return true;
                }
            }
            catch
            {
            }

            result = default;
            return false;
        }

        private static object? ParseByteSegment(byte[] data, FieldType type, ByteOrder order)
        {
            data = ApplyByteOrder(data, data.Length, order);
            return type switch
            {
                FieldType.Bool => data[0] != 0,
                FieldType.Byte => data[0],
                FieldType.Int16 => BitConverter.ToInt16(data, 0),
                FieldType.UInt16 => BitConverter.ToUInt16(data, 0),
                FieldType.Int32 => BitConverter.ToInt32(data, 0),
                FieldType.UInt32 => BitConverter.ToUInt32(data, 0),
                FieldType.Int64 => BitConverter.ToInt64(data, 0),
                FieldType.UInt64 => BitConverter.ToUInt64(data, 0),
                FieldType.Long => BitConverter.ToInt64(data, 0),
                FieldType.Float => BitConverter.ToSingle(data, 0),
                FieldType.Double => BitConverter.ToDouble(data, 0),
                _ => data
            };
        }

        private static byte[] ApplyByteOrder(byte[] bytes, int size, ByteOrder byteOrder)
        {
            if (bytes.Length < size)
            {
                throw new ArgumentException("字节数组长度不足");
            }

            byte[] result = new byte[size];
            Array.Copy(bytes, result, size);

            if (result.Length <= 1)
            {
                return result;
            }

            switch (byteOrder)
            {
                case ByteOrder.ABCD:
                    return result;
                case ByteOrder.BADC:
                    for (int i = 0; i < size - 1; i += 2)
                    {
                        (result[i], result[i + 1]) = (result[i + 1], result[i]);
                    }
                    return result;
                case ByteOrder.CDAB:
                    if (size == 4)
                    {
                        (result[0], result[1], result[2], result[3]) = (result[2], result[3], result[0], result[1]);
                    }
                    else if (size == 8)
                    {
                        (result[0], result[1], result[2], result[3], result[4], result[5], result[6], result[7]) =
                            (result[4], result[5], result[6], result[7], result[0], result[1], result[2], result[3]);
                    }
                    return result;
                case ByteOrder.DCBA:
                    Array.Reverse(result);
                    return result;
                default:
                    return result;
            }
        }
    }
}
