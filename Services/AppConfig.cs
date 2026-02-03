using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Logs;
using MeasurementSoftware.ViewModels;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace MeasurementSoftware.Services
{
    /// <summary>
    /// 应用程序全局配置
    /// 实现设备配置和配方配置两个接口
    /// 通过依赖注入使用，确保全局唯一
    /// </summary>
    public partial class AppConfig : ObservableViewModel, IDeviceConfigService, IRecipeConfigService
    {
        private readonly ILog _log;
        private readonly string _deviceConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "devices.json");

        #region 设备配置（IDeviceConfigService 实现）

        private ObservableCollection<PlcDevice> _devices = new();
        /// <summary>
        /// 系统中所有的 PLC 设备（全局）
        /// </summary>
        public ObservableCollection<PlcDevice> Devices
        {
            get => _devices;
            private set => SetProperty(ref _devices, value);
        }

        /// <summary>
        /// 加载设备配置
        /// </summary>
        public async Task LoadDevicesAsync()
        {
            try
            {
                if (File.Exists(_deviceConfigPath))
                {
                    var json = await File.ReadAllTextAsync(_deviceConfigPath);
                    var devices = JsonSerializer.Deserialize<List<PlcDevice>>(json);
                    if (devices != null)
                    {
                        Devices = new ObservableCollection<PlcDevice>(devices);
                        _log.Info($"已加载 {Devices.Count} 个PLC设备配置");
                    }
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_deviceConfigPath)!);
                    Devices = new ObservableCollection<PlcDevice>();
                    _log.Info("未找到PLC设备配置文件，将创建新配置");
                }
            }
            catch (Exception ex)
            {
                _log.Error($"加载PLC设备配置失败: {ex.Message}");
            }
        }

        public Task<List<PlcDevice>> GetAllDevicesAsync()
        {
            return Task.FromResult(Devices.ToList());
        }

        public Task<PlcDevice?> GetDeviceByIdAsync(string deviceId)
        {
            var device = Devices.FirstOrDefault(d => d.DeviceId == deviceId);
            return Task.FromResult(device);
        }

        public async Task<List<DataPoint>> GetDataPointsByDeviceIdAsync(string deviceId)
        {
            var device = await GetDeviceByIdAsync(deviceId);
            return device?.DataPoints.ToList() ?? new List<DataPoint>();
        }

        public async Task<bool> SaveDevicesAsync(List<PlcDevice> devices)
        {
            try
            {
                Devices = new ObservableCollection<PlcDevice>(devices);
                var json = JsonSerializer.Serialize(devices, new JsonSerializerOptions { WriteIndented = true });
                Directory.CreateDirectory(Path.GetDirectoryName(_deviceConfigPath)!);
                await File.WriteAllTextAsync(_deviceConfigPath, json);
                _log.Info($"已保存 {devices.Count} 个PLC设备配置");
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"保存PLC设备配置失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取指定设备的所有数据点（同步版本，用于兼容）
        /// </summary>
        public ObservableCollection<DataPoint> GetDataPointsByDeviceId(string deviceId)
        {
            var device = Devices.FirstOrDefault(d => d.DeviceId == deviceId);
            return device?.DataPoints ?? new ObservableCollection<DataPoint>();
        }

        #endregion

        #region 配方配置（IRecipeConfigService 实现）

        private MeasurementRecipe? _currentRecipe;
        /// <summary>
        /// 当前打开的配方（全局唯一）
        /// </summary>
        public MeasurementRecipe? CurrentRecipe
        {
            get => _currentRecipe;
            private set => SetProperty(ref _currentRecipe, value);
        }

        private string _currentRecipePath = string.Empty;
        /// <summary>
        /// 当前配方的文件路径
        /// </summary>
        public string CurrentRecipePath
        {
            get => _currentRecipePath;
            private set => SetProperty(ref _currentRecipePath, value);
        }

        /// <summary>
        /// 打开配方
        /// </summary>
        public void OpenRecipe(MeasurementRecipe recipe, string path)
        {
            CurrentRecipe = recipe;
            CurrentRecipePath = path;
            _log.Info($"已打开配方: {path}");
        }

        /// <summary>
        /// 关闭当前配方
        /// </summary>
        public void CloseRecipe()
        {
            CurrentRecipe = null;
            CurrentRecipePath = string.Empty;
            _log.Info("已关闭配方");
        }

        /// <summary>
        /// 更新配方路径
        /// </summary>
        public void UpdateRecipePath(string path)
        {
            CurrentRecipePath = path;
            _log.Info($"配方路径已更新: {path}");
        }

        /// <summary>
        /// 保存当前配方
        /// </summary>
        public async Task<bool> SaveCurrentRecipeAsync()
        {
            if (CurrentRecipe == null || string.IsNullOrEmpty(CurrentRecipePath))
            {
                _log.Info("没有打开的配方或配方路径为空");
                return false;
            }

            try
            {
                var json = JsonSerializer.Serialize(CurrentRecipe, new JsonSerializerOptions { WriteIndented = true });
                Directory.CreateDirectory(Path.GetDirectoryName(CurrentRecipePath)!);
                await File.WriteAllTextAsync(CurrentRecipePath, json);
                _log.Info($"配方已保存: {CurrentRecipePath}");
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"保存配方失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 加载配方
        /// </summary>
        public async Task<MeasurementRecipe?> LoadRecipeAsync(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    _log.Info($"配方文件不存在: {path}");
                    return null;
                }

                var json = await File.ReadAllTextAsync(path);
                var recipe = JsonSerializer.Deserialize<MeasurementRecipe>(json);
                _log.Info($"配方已加载: {path}");
                return recipe;
            }
            catch (Exception ex)
            {
                _log.Error($"加载配方失败: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region 构造函数

        public AppConfig(ILog log)
        {
            _log = log;
            _log.Info("AppConfig 已初始化");
        }

        #endregion
    }
}
