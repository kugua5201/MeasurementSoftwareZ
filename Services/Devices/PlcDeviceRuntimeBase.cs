using MeasurementSoftware.Models;
using MultiProtocol.Model;
using MultiProtocol.Services.IIndustrialProtocol;
using MultiProtocol.Utils;
using DriveType = MultiProtocol.Model.DriveType;

namespace MeasurementSoftware.Services.Devices
{
    /// <summary>
    /// PLC 运行时抽象基类。
    /// 提供协议初始化、连接、点位同步、读写等通用能力。
    /// </summary>
    public abstract class PlcDeviceRuntimeBase : IPlcDeviceRuntime
    {
        private IIndustrialProtocol? _protocol;

        protected PlcDeviceRuntimeBase(PlcDevice device)
        {
            Device = device ?? throw new ArgumentNullException(nameof(device));
        }

        /// <inheritdoc />
        public PlcDevice Device { get; }

        /// <summary>
        /// 当前协议实例。
        /// </summary>
        protected IIndustrialProtocol? Protocol => _protocol;

        /// <inheritdoc />
        public virtual async Task InitializeAsync()
        {
            await DestroyAsync();

            _protocol = CreateProtocol(BuildConnectionArgs(Device));
            if (_protocol == null)
            {
                return;
            }

            SubscribeProtocolEvents(_protocol);
        }

        /// <inheritdoc />
        public virtual async Task<(bool Success, string Message)> ConnectAsync()
        {
            if (_protocol == null)
            {
                return (false, "协议实例未初始化，请先调用 InitializeAsync");
            }

            try
            {
                var result = await Task.Run(() => _protocol.Connect());
                if (result.IsSuccess)
                {
                    Device.IsConnected = true;
                    ResetDevicePoints();

                    if (Device.IsEnabled)
                    {
                        _protocol.Open(true);
                    }

                    return (true, "连接成功");
                }

                Device.IsConnected = false;
                return (false, result.Message ?? "连接失败");
            }
            catch (Exception ex)
            {
                Device.IsConnected = false;
                return (false, $"连接异常: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public virtual async Task DisconnectAsync()
        {
            if (_protocol == null)
            {
                Device.IsConnected = false;
                Device.IsCacheReading = false;
                return;
            }

            try
            {
                _protocol.Open(false);
                await _protocol.DisconnectAsync();
            }
            catch
            {
            }
            finally
            {
                Device.IsConnected = false;
                Device.IsCacheReading = false;
            }
        }

        /// <inheritdoc />
        public virtual async Task DestroyAsync()
        {
            if (_protocol == null)
            {
                Device.IsConnected = false;
                Device.IsCacheReading = false;
                return;
            }

            try
            {
                await DisconnectAsync();
                UnsubscribeProtocolEvents(_protocol);
                _protocol.Dispose();
            }
            catch
            {
            }
            finally
            {
                _protocol = null;
                Device.IsConnected = false;
                Device.IsCacheReading = false;
            }
        }

        /// <inheritdoc />
        public virtual void ResetDevicePoints()
        {
            if (_protocol == null || Device.DataPoints.Count == 0)
            {
                return;
            }

            DeviceInfo deviceInfo = new(Device.DeviceId, Device.DeviceName);
            List<FieldInfo> fieldInfos = [];
            foreach (var dataPoint in Device.DataPoints)
            {
                fieldInfos.Add(new FieldInfo(dataPoint.Address, dataPoint.DataType, dataPoint.ByteOrder));
            }

            var checkedFields = DataFieldsHelper.CheckFileds(GetDriveType(), fieldInfos);
            _protocol.SetDevice(deviceInfo, checkedFields);
        }

        /// <inheritdoc />
        public virtual void SetPollingEnabled(bool enabled)
        {
            if (_protocol == null)
            {
                return;
            }

            try
            {
                _protocol.Open(enabled);
            }
            catch
            {
            }
        }

        /// <inheritdoc />
        public virtual async Task<object?> ReadDataPointValueAsync(DataPoint dataPoint)
        {
            ArgumentNullException.ThrowIfNull(dataPoint);

            if (_protocol == null)
            {
                return null;
            }

            var fieldInfo = new FieldInfo(dataPoint.Address, dataPoint.DataType, dataPoint.ByteOrder);
            var results = await _protocol.ReadDataAsync(Device.DeviceId, [fieldInfo]);
            return results.FirstOrDefault(r => r.IsSuccess)?.Value;
        }

        /// <inheritdoc />
        public virtual async Task<(bool Success, string? Message)> WriteDataPointValueAsync(DataPoint dataPoint, object value)
        {
            ArgumentNullException.ThrowIfNull(dataPoint);

            if (_protocol == null || !Device.IsConnected)
            {
                return (false, $"设备 [{Device.DeviceName}] 未连接");
            }

            var field = new FieldInfo(dataPoint.Address, dataPoint.DataType, dataPoint.ByteOrder)
            {
                Value = value
            };

            var results = await _protocol.WriteDataAsync(Device.DeviceId, [field]);
            var result = results.FirstOrDefault();
            return result != null && result.IsSuccess
                ? (true, null)
                : (false, result?.Message ?? "写入失败");
        }

        /// <summary>
        /// 创建协议实例。
        /// </summary>
        protected abstract IIndustrialProtocol? CreateProtocol(ConnectionArgs args);

        /// <summary>
        /// 返回当前协议对应的驱动类型。
        /// </summary>
        protected abstract DriveType GetDriveType();

        /// <summary>
        /// 订阅协议事件。
        /// </summary>
        protected virtual void SubscribeProtocolEvents(IIndustrialProtocol protocol)
        {
            protocol.OnDataRead += Protocol_OnDataRead;
            protocol.OnConnectChanged += Protocol_OnConnectChanged;
        }

        /// <summary>
        /// 取消订阅协议事件。
        /// </summary>
        protected virtual void UnsubscribeProtocolEvents(IIndustrialProtocol protocol)
        {
            protocol.OnDataRead -= Protocol_OnDataRead;
            protocol.OnConnectChanged -= Protocol_OnConnectChanged;
        }

        /// <summary>
        /// 协议读取事件默认处理：回填点位值。
        /// </summary>
        protected virtual void OnProtocolDataRead(DataEventArgs e)
        {
            foreach (var fieldInfo in e.Data)
            {
                var dataPoint = Device.DataPoints.FirstOrDefault(dp => dp.Address == fieldInfo.Address);
                if (dataPoint == null)
                {
                    continue;
                }

                dataPoint.IsSuccess = fieldInfo.IsSuccess;
                dataPoint.LastUpdateTime = fieldInfo.Time == default ? DateTime.Now : fieldInfo.Time;

                if (fieldInfo.IsSuccess)
                {
                    dataPoint.CurrentValue = fieldInfo.Value;
                    dataPoint.ErrorMessage = null;
                }
                else
                {
                    dataPoint.ErrorMessage = fieldInfo.Message;
                }
            }
        }

        /// <summary>
        /// 协议连接状态变化默认处理。
        /// </summary>
        protected virtual void OnProtocolConnectChanged(bool connected)
        {
            Device.IsConnected = connected;
            if (connected)
            {
                ResetDevicePoints();
                Protocol?.Open(Device.IsEnabled);
            }
            else
            {
                Device.IsCacheReading = false;
            }
        }

        /// <summary>
        /// 构建通用连接参数。
        /// </summary>
        protected static ConnectionArgs BuildConnectionArgs(PlcDevice device)
        {
            return new ConnectionArgs
            {
                IpAddress = device.IpAddress,
                Port = device.Port,
                PortName = device.ComPort,
                BaudRate = (int)device.BaudRate,
                DataBit = (int)device.DataBits,
                RtsEnable = device.RtsEnable,
                DtrEnable = device.DtrEnable,
                Slot = (byte)device.Slot,
                AutoReconnect = device.AutoReconnect,
                ReconnectInterval = device.ReconnectInterval,
                ConnectionTimeout = device.ConnectionTimeout,
                ReceiveTimeOut = device.ReceiveTimeout,
                OperationTimeout = device.OperationTimeout,
                PollingSpeed = device.PollingSpeed,
                MaxErrorConnections = device.MaxErrorConnections,
                MaxConnections = device.MaxConnections,
                AddressStartWithZero = device.AddressStartWithZero,
            };
        }

        private void Protocol_OnDataRead(object? sender, DataEventArgs e)
        {
            OnProtocolDataRead(e);
        }

        private void Protocol_OnConnectChanged(object? sender, bool connected)
        {
            OnProtocolConnectChanged(connected);
        }
    }
}
