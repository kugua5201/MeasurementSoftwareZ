using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Devices;
using MeasurementSoftware.Services.Logs;
using MeasurementSoftware.ViewModels;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MeasurementSoftware.Services.Config
{
    /// <summary>
    /// 应用程序全局配置（单例）
    /// 统一管理配方、设备、通道、二维码等所有跟随配方的配置
    /// </summary>
    public partial class AppConfig : ObservableViewModel, IRecipeConfigService, IDeviceConfigService, IQrCodeConfigService
    {

        private readonly ILog _log;
        private readonly IPlcDeviceRuntimeService _plcDeviceRuntimeService;

        public AppConfig(ILog log, IPlcDeviceRuntimeService plcDeviceRuntimeService)
        {
            _log = log;
            _plcDeviceRuntimeService = plcDeviceRuntimeService;
        }

        public string LastSaveErrorMessage { get; private set; } = string.Empty;

        #region 设备配置

        private ObservableCollection<PlcDevice> _devices = [];
        /// <summary>
        /// 系统中所有的 PLC 设备（来自当前配方）
        /// </summary>
        public ObservableCollection<PlcDevice> Devices
        {
            get => _devices;
            private set => SetProperty(ref _devices, value);
        }

        /// <summary>
        /// 初始化当前设备列表中所有PLC的协议实例并连接
        /// 在OpenRecipe后调用，从配方中加载设备并建立连接
        /// </summary>
        public async Task LoadDevicesAsync()
        {
            foreach (var device in Devices)
            {
                try
                {
                    await _plcDeviceRuntimeService.InitializeAsync(device);

                    var (success, message) = await _plcDeviceRuntimeService.ConnectAsync(device);

                    if (success)
                    {
                        _log.Info($"设备 [{device.DeviceName}] 连接成功");

                    }
                    else
                    {

                        _log.Warn($"设备 [{device.DeviceName}] 连接失败: {message}");
                    }

                }
                catch (Exception ex)
                {
                    _log.Error($"设备 [{device.DeviceName}] 初始化失败: {ex.Message}");
                }
            }
        }

        public Task<List<PlcDevice>> GetAllDevicesAsync()
        {
            return Task.FromResult(Devices.ToList());
        }

        public Task<PlcDevice?> GetDeviceByIdAsync(long deviceId)
        {
            var device = Devices.FirstOrDefault(d => d.DeviceId == deviceId);
            return Task.FromResult(device);
        }

        public async Task<List<DataPoint>> GetDataPointsByDeviceIdAsync(long deviceId)
        {
            var device = await GetDeviceByIdAsync(deviceId);
            return device?.DataPoints.ToList() ?? new List<DataPoint>();
        }

        public async Task<bool> SaveDevicesAsync(List<PlcDevice> devices)
        {
            try
            {
                // 没有配方时自动创建一个默认配方
                if (CurrentRecipe == null)
                {
                    var defaultRecipe = new MeasurementRecipe
                    {
                        BasicInfo = new RecipeBasicInfoConfig
                        {
                            RecipeId = Guid.NewGuid().ToString(),
                            RecipeName = $"默认配方_{DateTime.Now:yyyyMMddHHmmss}",
                            CreateTime = DateTime.Now,
                            ModifyTime = DateTime.Now
                        }
                    };
                    OpenRecipe(defaultRecipe, string.Empty);
                    _log.Info("保存设备时未找到配方，已自动创建默认配方");
                }

                var devicesToSave = new ObservableCollection<PlcDevice>(devices);
                SyncSiemensCacheConfigurations(devicesToSave);

                if (!ReferenceEquals(Devices, devicesToSave))
                {
                    Devices = devicesToSave;
                }

                // 同步到配方并保存
                CurrentRecipe!.Devices = Devices;
                var result = await SaveCurrentRecipeAsync();
                if (result)
                {
                    _log.Info($"已保存 {devices.Count} 个PLC设备配置到配方文件");
                }
                return result;
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
        public ObservableCollection<DataPoint> GetDataPointsByDeviceId(long deviceId)
        {
            var device = Devices.FirstOrDefault(d => d.DeviceId == deviceId);
            return device?.DataPoints ?? [];
        }

        #endregion

        #region 配方配置

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
        /// 当前配方的二维码配置（快捷访问）
        /// </summary>
        public QrCodeConfig QrCodeConfig
        {
            get => CurrentRecipe?.QrCodeConfig ?? new QrCodeConfig();
            set
            {
                if (CurrentRecipe != null)
                {
                    CurrentRecipe.QrCodeConfig = value;
                    OnPropertyChanged();
                }
            }
        }



        /// <summary>
        /// 保存二维码配置（实际保存整个配方）
        /// </summary>
        public Task<bool> SaveQrCodeConfigAsync() => SaveCurrentRecipeAsync();

        /// <summary>
        /// 打开配方：设置当前配方，同步设备列表和二维码配置
        /// </summary>
        public void OpenRecipe(MeasurementRecipe recipe, string path)
        {
            // 销毁旧设备的PLC连接
            foreach (var device in Devices)
            {
                try { _ = _plcDeviceRuntimeService.DestroyAsync(device); } catch { }
            }

            CurrentRecipePath = path;

            // 从配方中同步设备列表到运行时，保持 Devices 的引用不变
            Devices.Clear();
            if (recipe.Devices != null)
            {
                foreach (var device in recipe.Devices)
                {
                    device.SiemensReadCache?.ValidateAndApplyStructure();
                    Devices.Add(device);
                }
            }
            recipe.Devices = Devices; // 确保双向引用一致
            EnsureRecipeStatistics(recipe);
            HydrateChannelRuntimeData(recipe);

            CurrentRecipe = recipe;

            // 通知所有监听者：设备列表和二维码配置已随配方更新
            OnPropertyChanged(nameof(Devices));
            OnPropertyChanged(nameof(QrCodeConfig));

            _log.Info($"已打开配方: {recipe.BasicInfo.RecipeName}，包含 {Devices.Count} 个设备");
        }

        private void HydrateChannelRuntimeData(MeasurementRecipe recipe)
        {
            if (recipe.Channels == null)
            {
                return;
            }

            foreach (var channel in recipe.Channels)
            {
                if (channel.PlcDeviceId == 0)
                {
                    channel.ClearRuntimeBindings();
                    continue;
                }

                var device = Devices.FirstOrDefault(d => d.DeviceId == channel.PlcDeviceId);
                if (device == null)
                {
                    channel.ClearRuntimeBindings();
                    continue;
                }

                channel.BindDevice(device);
                channel.BindDataPoint(device.DataPoints.FirstOrDefault(dp => dp.PointId == channel.DataPointId));
            }
        }

        /// <summary>
        /// 关闭当前配方：销毁PLC连接，清空所有运行时状态
        /// </summary>
        public void CloseRecipe()
        {
            foreach (var device in Devices)
            {
                try { _ = _plcDeviceRuntimeService.DestroyAsync(device); } catch { }
            }
            Devices.Clear();

            CurrentRecipe = null;
            CurrentRecipePath = string.Empty;

            // 通知所有监听者：设备列表和二维码配置已清空
            OnPropertyChanged(nameof(Devices));
            OnPropertyChanged(nameof(QrCodeConfig));

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
        /// 保存当前配方（包含设备列表、二维码配置等所有内容）
        /// 如果配方路径为空（新建配方），自动分配默认路径
        /// </summary>
        public async Task<bool> SaveCurrentRecipeAsync()
        {
            LastSaveErrorMessage = string.Empty;

            if (CurrentRecipe == null)
            {
                LastSaveErrorMessage = "没有打开的配方，无法保存";
                _log.Warn("没有打开的配方，无法保存");
                return false;
            }

            if (!CurrentRecipe.TryValidateStepConfiguration(out var validationError))
            {
                LastSaveErrorMessage = validationError;
                _log.Warn($"保存配方前校验失败: {validationError}");
                return false;
            }

            // 路径为空时（新建配方尚未保存过），自动分配默认路径
            if (string.IsNullOrEmpty(CurrentRecipePath))
            {
                var recipesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Recipes");
                Directory.CreateDirectory(recipesDir);
                CurrentRecipePath = Path.Combine(recipesDir, $"{CurrentRecipe.BasicInfo.RecipeName}.json");
                _log.Info($"新配方自动分配路径: {CurrentRecipePath}");
            }

            try
            {
                // 同步运行时设备列表到配方
                SyncSiemensCacheConfigurations(Devices);
                CurrentRecipe.BasicInfo.ModifyTime = DateTime.Now;
                CurrentRecipe.Devices = Devices;

                var json = JsonSerializer.Serialize(CurrentRecipe, new JsonSerializerOptions { WriteIndented = true });
                Directory.CreateDirectory(Path.GetDirectoryName(CurrentRecipePath)!);
                await File.WriteAllTextAsync(CurrentRecipePath, json);
                _log.Info($"配方已保存: {CurrentRecipePath}（含 {Devices.Count} 个设备）");
                return true;
            }
            catch (Exception ex)
            {
                LastSaveErrorMessage = ex.Message;
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
                EnsureRecipeStatistics(recipe);
                _log.Info($"配方已加载: {path}");
                return recipe;
            }
            catch (Exception ex)
            {
                _log.Error($"加载配方失败: {ex.Message}");
                return null;
            }
        }

        private void SyncSiemensCacheConfigurations(IEnumerable<PlcDevice> devices)
        {
            foreach (var device in devices)
            {
                if (device.DeviceType is not PlcDeviceType.SiemensS7_1200 and not PlcDeviceType.SiemensS7_1500)
                {
                    continue;
                }

                var cacheConfig = device.SiemensReadCache;
                if (!cacheConfig.IsEnabled)
                {
                    continue;
                }

                var (success, message) = cacheConfig.ValidateAndApplyStructure();
                if (success)
                {
                    _log.Info($"设备 [{device.DeviceName}] 双缓冲配置已在保存前同步: {message}");
                }
                else
                {
                    _log.Warn($"设备 [{device.DeviceName}] 双缓冲配置校验未通过，已保留当前输入文本继续保存: {message}");
                }
            }
        }

        /// <summary>
        /// 确保配方统计信息对象存在，兼容旧版本配方文件。
        /// </summary>
        public void EnsureRecipeStatistics(MeasurementRecipe? recipe)
        {
            if (recipe == null)
            {
                return;
            }

            recipe.Statistics ??= new RecipeStatisticsConfig();
        }

        /// <summary>
        /// 重置指定配方的统计信息。
        /// 仅作用于当前传入的配方，不影响其他配方。
        /// </summary>
        public void ResetRecipeStatistics(MeasurementRecipe? recipe)
        {
            if (recipe == null)
            {
                return;
            }

            EnsureRecipeStatistics(recipe);
            recipe.Statistics.PassCount = 0;
            recipe.Statistics.FailCount = 0;
            recipe.Statistics.TotalCount = 0;
        }




        #endregion


        #region 全局参数
        public bool IsCollecting { get; private set; } = false;

        public int AcquisitionDelayMs
        {
            get => CurrentRecipe?.OtherSettings.AcquisitionDelayMs ?? 500;
            set
            {
                if (CurrentRecipe != null)
                {
                    CurrentRecipe.OtherSettings.AcquisitionDelayMs = Math.Max(1, value);
                }
            }
        }

        ObservableCollection<PlcDevice> IDeviceConfigService.Devices => Devices;

        public void SetCollect(bool Collect)
        {
            IsCollecting = Collect;
        }

        ObservableCollection<DataPoint> IDeviceConfigService.GetDataPointsByDeviceId(long deviceId)
        {
            return GetDataPointsByDeviceId(deviceId);
        }

        #endregion
    }
}
