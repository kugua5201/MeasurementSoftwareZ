using MeasurementSoftware.Models;
using System.Collections.ObjectModel;

namespace MeasurementSoftware.Services.Config
{
    /// <summary>
    /// 设备配置服务接口
    /// </summary>
    public interface IDeviceConfigService
    {
        ObservableCollection<PlcDevice> Devices { get; }
        Task<List<PlcDevice>> GetAllDevicesAsync();
        Task<PlcDevice?> GetDeviceByIdAsync(long deviceId);
        Task<List<DataPoint>> GetDataPointsByDeviceIdAsync(long deviceId);
        Task<bool> SaveDevicesAsync(List<PlcDevice> devices);
        ObservableCollection<DataPoint> GetDataPointsByDeviceId(long deviceId);
        Task LoadDevicesAsync();
    }
}
