using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services.Devices
{
    /// <summary>
    /// PLC 设备运行时管理服务。
    /// 对外保留统一入口，内部按设备类型分派到独立运行时实现。
    /// </summary>
    public class PlcDeviceRuntimeService : IPlcDeviceRuntimeService
    {
        private readonly Lock _syncRoot = new();
        private readonly IPlcDeviceRuntimeFactory _runtimeFactory;
        private readonly Dictionary<PlcDevice, IPlcDeviceRuntime> _runtimes = [];

        public PlcDeviceRuntimeService(IPlcDeviceRuntimeFactory runtimeFactory)
        {
            _runtimeFactory = runtimeFactory;
        }


        public async Task InitializeAsync(PlcDevice device)
        {
            ArgumentNullException.ThrowIfNull(device);

            await DestroyAsync(device);

            var runtime = _runtimeFactory.CreateRuntime(device);
            SetRuntime(device, runtime);
            await runtime.InitializeAsync();
        }


        public async Task<(bool Success, string Message)> ConnectAsync(PlcDevice device)
        {
            ArgumentNullException.ThrowIfNull(device);

            var runtime = GetRuntime(device);
            if (runtime == null)
            {
                return (false, "协议实例未初始化，请先调用 InitializeAsync");
            }

            return await runtime.ConnectAsync();
        }


        public async Task DisconnectAsync(PlcDevice device)
        {
            ArgumentNullException.ThrowIfNull(device);

            var runtime = GetRuntime(device);
            if (runtime == null)
            {
                device.IsConnected = false;
                device.IsCacheReading = false;
                return;
            }

            await runtime.DisconnectAsync();
        }


        public async Task DestroyAsync(PlcDevice device)
        {
            ArgumentNullException.ThrowIfNull(device);

            var runtime = GetRuntime(device);
            if (runtime == null)
            {
                device.IsConnected = false;
                device.IsCacheReading = false;
                return;
            }

            await runtime.DestroyAsync();
            RemoveRuntime(device);
        }


        public void ResetDevicePoints(PlcDevice device)
        {
            ArgumentNullException.ThrowIfNull(device);
            GetRuntime(device)?.ResetDevicePoints();
        }


        public void SetPollingEnabled(PlcDevice device, bool enabled)
        {
            ArgumentNullException.ThrowIfNull(device);
            GetRuntime(device)?.SetPollingEnabled(enabled);
        }


        public void StartCacheReading(PlcDevice device)
        {
            ArgumentNullException.ThrowIfNull(device);
            if (GetRuntime(device) is ICachePlcDeviceRuntime cacheRuntime)
            {
                cacheRuntime.StartCacheReading();
            }
        }


        public void StopCacheReading(PlcDevice device)
        {
            ArgumentNullException.ThrowIfNull(device);
            if (GetRuntime(device) is ICachePlcDeviceRuntime cacheRuntime)
            {
                cacheRuntime.StopCacheReading();
            }
            else
            {
                device.IsCacheReading = false;
            }
        }


        public double? GetCacheFieldValue(PlcDevice device, string cacheFieldId)
        {
            ArgumentNullException.ThrowIfNull(device);
            return GetRuntime(device) is ICachePlcDeviceRuntime cacheRuntime ? cacheRuntime.GetCacheFieldValue(cacheFieldId) : null;
        }


        public IReadOnlyList<double> TakeCacheFieldValues(PlcDevice device, string cacheFieldId)
        {
            ArgumentNullException.ThrowIfNull(device);
            return GetRuntime(device) is ICachePlcDeviceRuntime cacheRuntime
                ? cacheRuntime.TakeCacheFieldValues(cacheFieldId)
                : [];
        }


        public async Task<object?> ReadDataPointValueAsync(PlcDevice device, DataPoint dataPoint)
        {
            ArgumentNullException.ThrowIfNull(device);
            ArgumentNullException.ThrowIfNull(dataPoint);

            var runtime = GetRuntime(device);
            return runtime == null ? null : await runtime.ReadDataPointValueAsync(dataPoint);
        }


        public async Task<(bool Success, string? Message)> WriteDataPointValueAsync(PlcDevice device, DataPoint dataPoint, object value)
        {
            ArgumentNullException.ThrowIfNull(device);
            ArgumentNullException.ThrowIfNull(dataPoint);

            var runtime = GetRuntime(device);
            return runtime == null ? (false, "协议实例未初始化，请先调用 InitializeAsync") : await runtime.WriteDataPointValueAsync(dataPoint, value);
        }

        private IPlcDeviceRuntime? GetRuntime(PlcDevice device)
        {
            lock (_syncRoot)
            {
                return _runtimes.TryGetValue(device, out var runtime) ? runtime : null;
            }
        }

        private void SetRuntime(PlcDevice device, IPlcDeviceRuntime runtime)
        {
            lock (_syncRoot)
            {
                _runtimes[device] = runtime;
            }
        }

        private void RemoveRuntime(PlcDevice device)
        {
            lock (_syncRoot)
            {
                _runtimes.Remove(device);
            }
        }
    }
}
