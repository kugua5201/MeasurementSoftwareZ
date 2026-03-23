using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Appearance;
using MeasurementSoftware.Services.Config;
using MeasurementSoftware.Services.Logs;
using MeasurementSoftware.Services.UserSetting;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using Forms = System.Windows.Forms;
using System.Windows;
using MessageBox = HandyControl.Controls.MessageBox;

namespace MeasurementSoftware.ViewModels
{
    public partial class OtherSettingsViewModel : ObservableViewModel
    {
        private readonly ILog _log;
        private readonly IRecipeConfigService _recipeConfigService;
        private readonly IAppAppearanceService _appAppearanceService;

        [ObservableProperty]
        private AcquisitionCsvColumnDefinition? selectedAvailableCsvColumn;

        [ObservableProperty]
        private AcquisitionCsvColumnConfig? selectedConfiguredCsvColumn;

        [ObservableProperty]
        private double appFontSize;

        public MeasurementRecipe? CurrentRecipe => _recipeConfigService.CurrentRecipe;
        public bool HasRecipe => CurrentRecipe != null;
        public ObservableCollection<AcquisitionCsvColumnDefinition> AvailableCsvColumns { get; } = [.. AcquisitionCsvColumnCatalog.All];
        public string AppFontFamilyName => _appAppearanceService.CurrentFontFamily;

        public OtherSettingsViewModel(ILog log, IRecipeConfigService recipeConfigService, IUserSettingsService userSettingsService, IAppAppearanceService appAppearanceService)
        {
            _log = log;
            _recipeConfigService = recipeConfigService;
            _appAppearanceService = appAppearanceService;
            appFontSize = userSettingsService.Settings.Appearance.FontSize;

            if (_recipeConfigService is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(IRecipeConfigService.CurrentRecipe))
                    {
                        OnPropertyChanged(nameof(CurrentRecipe));
                        OnPropertyChanged(nameof(HasRecipe));
                        OnPropertyChanged(nameof(ConfiguredCsvColumns));
                    }
                };
            }
        }

        partial void OnAppFontSizeChanged(double value)
        {
            _appAppearanceService.UpdateFontSize(value);
        }

        public ObservableCollection<AcquisitionCsvColumnConfig> ConfiguredCsvColumns => CurrentRecipe?.OtherSettings.AcquisitionStorage.CsvColumns ?? [];

        [RelayCommand]
        private async Task SaveRecipeAsync()
        {
            if (CurrentRecipe == null)
            {
                Growl.Warning("请先选择一个配方");
                return;
            }

            CurrentRecipe.BasicInfo.ModifyTime = DateTime.Now;
            var success = await _recipeConfigService.SaveCurrentRecipeAsync();
            if (success)
                Growl.Success("配方保存成功");
            else
                Growl.Error("配方保存失败");
        }

        [RelayCommand]
        private void SelectStorageFolder()
        {
            if (CurrentRecipe == null)
            {
                Growl.Warning("请先选择一个配方");
                return;
            }

            using var dialog = new Forms.FolderBrowserDialog
            {
                Description = "选择采集结果存储目录",
                InitialDirectory = string.IsNullOrWhiteSpace(CurrentRecipe.OtherSettings.AcquisitionStorage.OutputFolder)
                    ? AppDomain.CurrentDomain.BaseDirectory
                    : CurrentRecipe.OtherSettings.AcquisitionStorage.OutputFolder,
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == Forms.DialogResult.OK)
            {
                CurrentRecipe.OtherSettings.AcquisitionStorage.OutputFolder = dialog.SelectedPath;
            }
        }

        [RelayCommand]
        private void AddCsvColumn()
        {
            if (CurrentRecipe == null)
            {
                Growl.Warning("请先选择一个配方");
                return;
            }

            if (SelectedAvailableCsvColumn == null)
            {
                Growl.Warning("请先选择一个可添加的列");
                return;
            }

            if (ConfiguredCsvColumns.Any(c => c.Key == SelectedAvailableCsvColumn.Key))
            {
                Growl.Warning("该列已存在");
                return;
            }

            var config = AcquisitionCsvColumnCatalog.ToConfig(SelectedAvailableCsvColumn);
            ConfiguredCsvColumns.Add(config);
            SelectedConfiguredCsvColumn = config;
            OnPropertyChanged(nameof(ConfiguredCsvColumns));
        }

        [RelayCommand]
        private void RemoveCsvColumn()
        {
            if (CurrentRecipe == null)
            {
                Growl.Warning("请先选择一个配方");
                return;
            }

            if (SelectedConfiguredCsvColumn == null)
            {
                Growl.Warning("请先选择要移除的列");
                return;
            }

            ConfiguredCsvColumns.Remove(SelectedConfiguredCsvColumn);
            SelectedConfiguredCsvColumn = ConfiguredCsvColumns.FirstOrDefault();
            OnPropertyChanged(nameof(ConfiguredCsvColumns));
        }

        [RelayCommand]
        private void MoveCsvColumnUp()
        {
            if (SelectedConfiguredCsvColumn == null)
            {
                Growl.Warning("请先选择要上移的列");
                return;
            }

            var index = ConfiguredCsvColumns.IndexOf(SelectedConfiguredCsvColumn);
            if (index <= 0)
            {
                return;
            }

            ConfiguredCsvColumns.Move(index, index - 1);
            //OnPropertyChanged(nameof(ConfiguredCsvColumns));
        }

        [RelayCommand]
        private void MoveCsvColumnDown()
        {
            if (SelectedConfiguredCsvColumn == null)
            {
                Growl.Warning("请先选择要下移的列");
                return;
            }

            var index = ConfiguredCsvColumns.IndexOf(SelectedConfiguredCsvColumn);
            if (index < 0 || index >= ConfiguredCsvColumns.Count - 1)
            {
                return;
            }

            ConfiguredCsvColumns.Move(index, index + 1);
            // OnPropertyChanged(nameof(ConfiguredCsvColumns));
        }

        [RelayCommand]
        private void ResetCsvColumns()
        {
            if (CurrentRecipe == null)
            {
                Growl.Warning("请先选择一个配方");
                return;
            }
            var res = MessageBox.Show("确定要重置CSV列配置吗？这将清除当前的列设置并恢复默认列。", "确认重置", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res == MessageBoxResult.Yes)
            {


                var defaultColumns = AcquisitionCsvColumnCatalog.CreateDefaultSelection();
                CurrentRecipe.OtherSettings.AcquisitionStorage.CsvColumns = new ObservableCollection<AcquisitionCsvColumnConfig>(defaultColumns);
                SelectedConfiguredCsvColumn = CurrentRecipe.OtherSettings.AcquisitionStorage.CsvColumns.FirstOrDefault();
                OnPropertyChanged(nameof(ConfiguredCsvColumns));
            }
        }
    }
}
