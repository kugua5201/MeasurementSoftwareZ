using MeasurementSoftware.Models;
using System.Collections.ObjectModel;

namespace MeasurementSoftware.Services
{
    /// <summary>
    /// 设备配置服务接口
    /// </summary>
    public interface IDeviceConfigService
    {
        /// <summary>
        /// 所有 PLC 设备（只读）
        /// </summary>
        ObservableCollection<PlcDevice> Devices { get; }

        /// <summary>
        /// 获取所有设备
        /// </summary>
        Task<List<PlcDevice>> GetAllDevicesAsync();

        /// <summary>
        /// 根据设备ID获取设备
        /// </summary>
        Task<PlcDevice?> GetDeviceByIdAsync(string deviceId);

        /// <summary>
        /// 获取指定设备的所有数据点
        /// </summary>
        Task<List<DataPoint>> GetDataPointsByDeviceIdAsync(string deviceId);

        /// <summary>
        /// 保存设备配置
        /// </summary>
        Task<bool> SaveDevicesAsync(List<PlcDevice> devices);

        /// <summary>
        /// 获取指定设备的所有数据点（同步版本）
        /// </summary>
        ObservableCollection<DataPoint> GetDataPointsByDeviceId(string deviceId);

        /// <summary>
        /// 加载设备配置
        /// </summary>
        Task LoadDevicesAsync();
    }
}
