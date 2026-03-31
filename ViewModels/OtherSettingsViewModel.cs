using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Config;
using MeasurementSoftware.Services.Devices;
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
        private readonly ObservableCollection<PlcDevice> _enabledStepOperationDevices = [];
        private ObservableCollection<StepOperationBindingConfig>? _observedStepOperationBindings;

        [ObservableProperty]
        private AcquisitionCsvColumnDefinition? selectedAvailableCsvColumn;

        [ObservableProperty]
        private AcquisitionCsvColumnConfig? selectedConfiguredCsvColumn;

        public MeasurementRecipe? CurrentRecipe => _recipeConfigService.CurrentRecipe;
        public bool HasRecipe => CurrentRecipe != null;
        public ObservableCollection<AcquisitionCsvColumnDefinition> AvailableCsvColumns { get; } = [.. AcquisitionCsvColumnCatalog.All];

        /// <summary>
        /// 工步操作可选设备。
        /// 仅显示已启用设备，并随设备启用状态实时联动。
        /// </summary>
        public ObservableCollection<PlcDevice> StepOperationDevices => _enabledStepOperationDevices;

        /// <summary>
        /// 当前配方的工步操作绑定集合。
        /// </summary>
        public ObservableCollection<StepOperationBindingConfig> StepOperationBindings => CurrentRecipe?.OtherSettings.StepOperationBindings ?? [];

        /// <summary>
        /// 工步触发方式枚举。
        /// </summary>
        public Array StepOperationTriggerModes => Enum.GetValues<StepOperationTriggerMode>();

        public OtherSettingsViewModel(ILog log, IRecipeConfigService recipeConfigService, IDeviceConfigService deviceConfigService)
        {
            _log = log;
            _recipeConfigService = recipeConfigService;
            _deviceConfigService = deviceConfigService;

            if (_recipeConfigService is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(IRecipeConfigService.CurrentRecipe))
                    {
                        RefreshStepOperationDeviceState();
                        CurrentRecipe?.OtherSettings.HydrateStepOperationBindings(StepOperationDevices);
                        RebindStepOperationBindingNotifications();
                        OnPropertyChanged(nameof(CurrentRecipe));
                        OnPropertyChanged(nameof(HasRecipe));
                        OnPropertyChanged(nameof(ConfiguredCsvColumns));
                        OnPropertyChanged(nameof(StepOperationBindings));
                    }
                };
            }

            if (_deviceConfigService is INotifyPropertyChanged deviceNpc)
            {
                deviceNpc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(IDeviceConfigService.Devices))
                    {
                        RebindDeviceCollectionNotifications();
                        RefreshStepOperationDeviceState();
                        CurrentRecipe?.OtherSettings.HydrateStepOperationBindings(StepOperationDevices);
                        OnPropertyChanged(nameof(StepOperationDevices));
                    }
                };
            }

            RebindDeviceCollectionNotifications();
            RefreshStepOperationDeviceState();
            CurrentRecipe?.OtherSettings.HydrateStepOperationBindings(StepOperationDevices);
            RebindStepOperationBindingNotifications();
        }

        private void RebindStepOperationBindingNotifications()
        {
            if (_observedStepOperationBindings != null)
            {
                _observedStepOperationBindings.CollectionChanged -= StepOperationBindings_CollectionChanged;
                foreach (var binding in _observedStepOperationBindings)
                {
                    binding.PropertyChanged -= StepOperationBinding_PropertyChanged;
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
                binding.PropertyChanged -= StepOperationBinding_PropertyChanged;
                binding.PropertyChanged += StepOperationBinding_PropertyChanged;
                SyncStepOperationBindingRuntime(binding);
            }
        }

        private void StepOperationBindings_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (StepOperationBindingConfig binding in e.OldItems)
                {
                    binding.PropertyChanged -= StepOperationBinding_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (StepOperationBindingConfig binding in e.NewItems)
                {
                    binding.PropertyChanged -= StepOperationBinding_PropertyChanged;
                    binding.PropertyChanged += StepOperationBinding_PropertyChanged;
                    SyncStepOperationBindingRuntime(binding);
                }
            }
        }

        private void StepOperationBinding_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not StepOperationBindingConfig binding)
            {
                return;
            }

            if (e.PropertyName == nameof(StepOperationBindingConfig.PlcDeviceId))
            {
                SyncStepOperationBindingRuntime(binding);
            }
            else if (e.PropertyName == nameof(StepOperationBindingConfig.DataPointId))
            {
                binding.SyncRuntimeDataPointReference();
            }
        }

        private void SyncStepOperationBindingRuntime(StepOperationBindingConfig binding)
        {
            var device = StepOperationDevices.FirstOrDefault(d => d.DeviceId == binding.PlcDeviceId);
            if (device == null)
            {
                binding.HydrateRuntimeBindings(null);
                return;
            }

            if (!string.IsNullOrEmpty(binding.DataPointId)
                && !device.DataPoints.Any(dp => dp.IsEnabled && dp.PointId == binding.DataPointId))
            {
                binding.DataPointId = string.Empty;
                return;
            }

            binding.HydrateRuntimeBindings(device);
        }

        private void RebindDeviceCollectionNotifications()
        {
            _deviceConfigService.Devices.CollectionChanged -= Devices_CollectionChanged;
            _deviceConfigService.Devices.CollectionChanged += Devices_CollectionChanged;

            foreach (var device in _deviceConfigService.Devices)
            {
                device.PropertyChanged -= Device_PropertyChanged;
                device.PropertyChanged += Device_PropertyChanged;
            }
        }

        private void Devices_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (PlcDevice device in e.OldItems)
                {
                    device.PropertyChanged -= Device_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (PlcDevice device in e.NewItems)
                {
                    device.PropertyChanged -= Device_PropertyChanged;
                    device.PropertyChanged += Device_PropertyChanged;
                }
            }

            RefreshStepOperationDeviceState();
        }

        private void Device_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(PlcDevice.IsEnabled))
            {
                return;
            }

            RefreshStepOperationDeviceState();
        }

        private void RefreshStepOperationDeviceState()
        {
            var enabledDevices = _deviceConfigService.Devices.Where(device => device.IsEnabled).ToList();

            _enabledStepOperationDevices.Clear();
            foreach (var device in enabledDevices)
            {
                _enabledStepOperationDevices.Add(device);
            }

            CurrentRecipe?.OtherSettings.HydrateStepOperationBindings(_enabledStepOperationDevices);
            OnPropertyChanged(nameof(StepOperationDevices));
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
