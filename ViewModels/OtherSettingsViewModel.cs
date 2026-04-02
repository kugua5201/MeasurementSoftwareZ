using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Config;
using MeasurementSoftware.Services.Logs;
using System.Collections.Specialized;
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
        private readonly IDeviceConfigService _deviceConfigService;
        private readonly EnabledPlcDevicesObserver _enabledDevicesObserver;

        private ObservableCollection<StepOperationBindingConfig>? _observedStepOperationBindings;

        [ObservableProperty]
        private AcquisitionCsvColumnDefinition? selectedAvailableCsvColumn;

        /// <summary>
        /// 当前选中的已配置导出列。
        /// </summary>
        [ObservableProperty]
        private AcquisitionCsvColumnConfig? selectedConfiguredCsvColumn;

        /// <summary>
        /// 当前打开的配方。
        /// </summary>
        public MeasurementRecipe? CurrentRecipe => _recipeConfigService.CurrentRecipe;

        /// <summary>
        /// 当前是否存在已打开配方。
        /// </summary>
        public bool HasRecipe => CurrentRecipe != null;

        /// <summary>
        /// 所有可添加的导出列定义。
        /// </summary>
        public ObservableCollection<AcquisitionCsvColumnDefinition> AvailableCsvColumns { get; } = [.. AcquisitionCsvColumnCatalog.All];

        /// <summary>
        /// 工步操作可选设备。
        /// 仅显示已启用设备，并随设备启用状态实时联动。
        /// </summary>
        public ReadOnlyObservableCollection<PlcDevice> StepOperationDevices => _enabledDevicesObserver.EnabledDevicesView;

        /// <summary>
        /// 当前配方的工步操作绑定集合。
        /// </summary>
        public ObservableCollection<StepOperationBindingConfig> StepOperationBindings => CurrentRecipe?.OtherSettings.StepOperationBindings ?? [];

        /// <summary>
        /// 工步触发方式枚举。
        /// </summary>
        public Array StepOperationTriggerModes => Enum.GetValues<StepOperationTriggerMode>();

        /// <summary>
        /// 创建其他设置页面的视图模型。
        /// </summary>
        public OtherSettingsViewModel(ILog log, IRecipeConfigService recipeConfigService, IDeviceConfigService deviceConfigService)
        {
            _log = log;
            _recipeConfigService = recipeConfigService;
            _deviceConfigService = deviceConfigService;
            _enabledDevicesObserver = new EnabledPlcDevicesObserver(_deviceConfigService);

            if (_recipeConfigService is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(IRecipeConfigService.CurrentRecipe))
                    {
                        _enabledDevicesObserver.Rebind();
                        CurrentRecipe?.OtherSettings.HydrateStepOperationBindings(_enabledDevicesObserver.EnabledDevices);
                        RebindStepOperationBindingNotifications();
                        OnPropertyChanged(nameof(CurrentRecipe));
                        OnPropertyChanged(nameof(HasRecipe));
                        OnPropertyChanged(nameof(ConfiguredCsvColumns));
                        OnPropertyChanged(nameof(StepOperationBindings));
                    }
                };
            }

            _enabledDevicesObserver.Changed += (s, e) =>
            {
                CurrentRecipe?.OtherSettings.HydrateStepOperationBindings(_enabledDevicesObserver.EnabledDevices);
            };

            _enabledDevicesObserver.Rebind();
            CurrentRecipe?.OtherSettings.HydrateStepOperationBindings(_enabledDevicesObserver.EnabledDevices);
            RebindStepOperationBindingNotifications();
        }

        /// <summary>
        /// 重新绑定当前配方的工步操作绑定集合监听。
        /// 配方切换后需要重建绑定项与可选设备集合的关系。
        /// </summary>
        private void RebindStepOperationBindingNotifications()
        {
            if (_observedStepOperationBindings != null)
            {
                _observedStepOperationBindings.CollectionChanged -= StepOperationBindings_CollectionChanged;
                foreach (var binding in _observedStepOperationBindings)
                {
                    binding.DetachAvailableDevices();
                }
            }

            _observedStepOperationBindings = CurrentRecipe?.OtherSettings.StepOperationBindings;
            if (_observedStepOperationBindings == null)
            {
                return;
            }

            _observedStepOperationBindings.CollectionChanged -= StepOperationBindings_CollectionChanged;
            _observedStepOperationBindings.CollectionChanged += StepOperationBindings_CollectionChanged;
            foreach (var binding in _observedStepOperationBindings)
            {
                binding.AttachAvailableDevices(_enabledDevicesObserver.EnabledDevices);
            }
        }

        /// <summary>
        /// 处理工步绑定集合的增删，保证新增项也能接入设备集合监听。
        /// </summary>
        private void StepOperationBindings_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (StepOperationBindingConfig binding in e.OldItems)
                {
                    binding.DetachAvailableDevices();
                }
            }

            if (e.NewItems != null)
            {
                foreach (StepOperationBindingConfig binding in e.NewItems)
                {
                    binding.AttachAvailableDevices(_enabledDevicesObserver.EnabledDevices);
                }
            }
        }

        /// <summary>
        /// 当前已配置的 CSV 导出列。
        /// </summary>
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
                Growl.Warning("配方保存失败");
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
