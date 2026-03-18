using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Config;
using MeasurementSoftware.Services.Logs;
using MeasurementSoftware.Services.UserSetting;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace MeasurementSoftware.ViewModels
{
    public partial class RecipeManagementViewModel : ObservableViewModel
    {
        private readonly ILog _log;
        private readonly IRecipeConfigService _recipeConfigService;
        private readonly IDeviceConfigService _deviceConfigService;
        private readonly IUserSettingsService _userSettingsService;

        /// <summary>
        /// 当前配方（来自全局配置）
        /// </summary>
        public MeasurementRecipe? CurrentRecipe => _recipeConfigService.CurrentRecipe;

        /// <summary>
        /// 当前配方路径
        /// </summary>
        public string CurrentRecipePath => _recipeConfigService.CurrentRecipePath;

        /// <summary>
        /// 是否有配方打开
        /// </summary>
        public bool HasRecipe => CurrentRecipe != null;

        /// <summary>
        /// 配方概要信息
        /// </summary>
        public string RecipeSummary
        {
            get
            {
                if (CurrentRecipe == null) return "未打开配方";
                var channelCount = CurrentRecipe.Channels?.Count ?? 0;
                var enabledCount = CurrentRecipe.Channels?.Count(c => c.IsEnabled) ?? 0;
                var deviceCount = CurrentRecipe.Devices?.Count ?? 0;
                return $"通道: {enabledCount}/{channelCount} 启用  |  设备: {deviceCount} 个  |  " +
                       $"创建: {CurrentRecipe.CreateTime:yyyy-MM-dd HH:mm}  |  " +
                       $"修改: {CurrentRecipe.ModifyTime:yyyy-MM-dd HH:mm}";
            }
        }

        public RecipeManagementViewModel(ILog log, IRecipeConfigService recipeConfigService, IDeviceConfigService deviceConfigService, IUserSettingsService userSettingsService)
        {
            _log = log;
            _recipeConfigService = recipeConfigService;
            _deviceConfigService = deviceConfigService;
            _userSettingsService = userSettingsService;

            // 监听配方变化
            if (_recipeConfigService is System.ComponentModel.INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(IRecipeConfigService.CurrentRecipe) ||
                        e.PropertyName == nameof(IRecipeConfigService.CurrentRecipePath))
                    {
                        NotifyRecipeChanged();
                    }
                };
            }
        }

        private void NotifyRecipeChanged()
        {
            OnPropertyChanged(nameof(CurrentRecipe));
            OnPropertyChanged(nameof(CurrentRecipePath));
            OnPropertyChanged(nameof(HasRecipe));
            OnPropertyChanged(nameof(RecipeSummary));
        }


        /// <summary>
        /// 新建配方
        /// </summary>
        [RelayCommand]
        public virtual void CreateNewRecipe()
        {

            if (CurrentRecipe != null)
            {
                var result = HandyControl.Controls.MessageBox.Show(
                    "当前有打开的配方，新建将关闭当前配方。未保存的更改将丢失，是否继续？",
                    "确认新建", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;
            }

            var newRecipe = new MeasurementRecipe
            {
                RecipeId = Guid.NewGuid().ToString(),
                RecipeName = $"新配方_{DateTime.Now:yyyyMMddHHmmss}",
                CreateTime = DateTime.Now,
                ModifyTime = DateTime.Now,
                Devices = new ObservableCollection<PlcDevice>(),
                QrCodeConfig = new QrCodeConfig()
            };
            _recipeConfigService.OpenRecipe(newRecipe, string.Empty);
            NotifyRecipeChanged();
            Growl.Success("已创建新配方，请在各配置页面进行设置");
            _log.Info($"新建配方: {newRecipe.RecipeName}");
        }

        /// <summary>
        /// 打开配方
        /// </summary>
        [RelayCommand]
        public virtual async Task OpenRecipeAsync()
        {

            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "配方文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                    Title = "打开配方"
                };

                var lastRecipeDirectory = Path.GetDirectoryName(_userSettingsService.Settings.LastRecipePath);
                if (!string.IsNullOrWhiteSpace(lastRecipeDirectory) &&
                    Directory.Exists(lastRecipeDirectory))
                {
                    dialog.InitialDirectory = lastRecipeDirectory;
                }

                if (dialog.ShowDialog() == true)
                {
                    var recipe = await _recipeConfigService.LoadRecipeAsync(dialog.FileName);
                    if (recipe != null)
                    {
                        _recipeConfigService.OpenRecipe(recipe, dialog.FileName);

                        // 初始化配方中的PLC设备连接
                        await _deviceConfigService.LoadDevicesAsync();

                        // 记住最后打开的配方路径
                        _userSettingsService.Settings.LastRecipePath = dialog.FileName;
                        _userSettingsService.SaveSettings();

                        NotifyRecipeChanged();
                        Growl.Success($"配方 {recipe.RecipeName} 加载成功");
                        _log.Info($"打开配方: {recipe.RecipeName}");
                    }
                    else
                    {
                        Growl.Error("配方加载失败");
                        _log.Error("配方加载失败");
                    }
                }
            }
            catch (Exception ex)
            {
                Growl.Error($"打开配方失败: {ex.Message}");
                _log.Error($"打开配方异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存配方
        /// </summary>
        [RelayCommand]
        private async Task SaveRecipeAsync()
        {
            if (CurrentRecipe == null)
            {
                Growl.Warning("没有配方需要保存");
                return;
            }

            try
            {
                if (!string.IsNullOrEmpty(CurrentRecipePath))
                {
                    // 配方名称与文件名保持一致
                    CurrentRecipe.RecipeName = Path.GetFileNameWithoutExtension(CurrentRecipePath);
                    CurrentRecipe.ModifyTime = DateTime.Now;
                    var success = await _recipeConfigService.SaveCurrentRecipeAsync();
                    if (success)
                    {
                        // 更新用户设置
                        _userSettingsService.Settings.LastRecipePath = CurrentRecipePath;
                        _userSettingsService.SaveSettings();

                        NotifyRecipeChanged();
                        Growl.Success("配方保存成功");
                        _log.Info($"配方 {CurrentRecipe.RecipeName} 保存成功");
                    }
                    else
                    {
                        Growl.Error("配方保存失败");
                    }
                }
                else
                {
                    await SaveAsRecipeAsync();
                }
            }
            catch (Exception ex)
            {
                Growl.Error($"保存配方异常: {ex.Message}");
                _log.Error($"保存配方异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 另存为配方
        /// </summary>
        [RelayCommand]
        public virtual async Task SaveAsRecipeAsync()
        {


            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "配方文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                    Title = "另存为配方",
                    FileName = CurrentRecipe?.RecipeName + ".json"
                };

                var lastRecipeDirectory = Path.GetDirectoryName(_userSettingsService.Settings.LastRecipePath);
                if (!string.IsNullOrWhiteSpace(lastRecipeDirectory) &&
                    Directory.Exists(lastRecipeDirectory))
                {
                    dialog.InitialDirectory = lastRecipeDirectory;
                }

                if (dialog.ShowDialog() == true)
                {
                    // 配方名称与文件名保持一致
                    CurrentRecipe?.RecipeName = Path.GetFileNameWithoutExtension(dialog.FileName);
                    CurrentRecipe?.ModifyTime = DateTime.Now;
                    _recipeConfigService.UpdateRecipePath(dialog.FileName);
                    var success = await _recipeConfigService.SaveCurrentRecipeAsync();
                    if (success)
                    {
                        _userSettingsService.Settings.LastRecipePath = dialog.FileName;
                        _userSettingsService.SaveSettings();

                        NotifyRecipeChanged();
                        Growl.Success($"配方另存为 {Path.GetFileName(dialog.FileName)} 成功");
                        _log.Info($"配方另存为: {dialog.FileName}");
                    }
                    else
                    {
                        Growl.Error("配方保存失败");
                    }
                }
            }
            catch (Exception ex)
            {
                Growl.Error($"另存为异常: {ex.Message}");
                _log.Error($"另存为异常: {ex.Message}");
            }
        }


        private bool CheckAcquiring()
        {
            if (_recipeConfigService.IsCollecting)
            {
                Growl.Warning("当前正在采集中，无法进行操作");
                return false;
            }
            return true;
        }
    }
}
