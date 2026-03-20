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
            }

            Protocol.StartCacheReading(
                $"{cache.Cache1.DbBlock}", cache.Cache1.Length, cache.Cache1.ReadableFlagAddress,
                $"{cache.Cache2.DbBlock}", cache.Cache2.Length, cache.Cache2.ReadableFlagAddress);
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

            foreach (var expandedField in cache.ExpandedFieldDefinitions.Where(f => f.CacheIndex == data.CacheIndex))
            {
                int offset = expandedField.GroupIndex * cache.GroupSize + expandedField.Offset;
                int size = SiemensReadCacheConfig.GetFieldTypeSize(expandedField.DataType);
                if (offset + size > data.Data.Length)
                {
                    expandedField.ParsedValue = null;
                    expandedField.Description = $"缓存长度不足，当前{data.Data.Length}字节，至少需要{offset + size}字节";
                    expandedField.LastUpdateTime = now;
                    _cacheFieldValues.TryRemove(expandedField.CacheFieldKey, out _);
                    continue;
                }

                try
                {
                    byte[] segment = new byte[size];
                    Array.Copy(data.Data, offset, segment, 0, size);
                    var value = ParseByteSegment(segment, expandedField.DataType, expandedField.ByteOrder);
                    expandedField.ParsedValue = value;
                    expandedField.Description = string.Empty;
                    expandedField.LastUpdateTime = now;
                    _cacheFieldValues[expandedField.CacheFieldKey] = value;
                }
                catch (Exception ex)
                {
                    expandedField.ParsedValue = null;
                    expandedField.Description = $"解析失败: {ex.Message}";
                    expandedField.LastUpdateTime = now;
                    _cacheFieldValues.TryRemove(expandedField.CacheFieldKey, out _);
                }
            }
        }

        private void SetCacheFieldDescriptions(int cacheIndex, string message)
        {
            var now = DateTime.Now;
            foreach (var field in Device.SiemensReadCache.ExpandedFieldDefinitions.Where(f => f.CacheIndex == cacheIndex))
            {
                field.Description = message;
                field.ParsedValue = null;
                field.LastUpdateTime = now;
                _cacheFieldValues.TryRemove(field.CacheFieldKey, out _);
            }
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
