using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services;
using MeasurementSoftware.Services.Logs;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Channels;

namespace MeasurementSoftware.ViewModels
{
    public partial class HomeViewModel : ObservableViewModel
    {
        private readonly ILog _log;
        private readonly IRecipeConfigService _recipeConfigService;
        private readonly IUserSettingsService _userSettingsService;

        [ObservableProperty]
        private string? productImagePath;

        [ObservableProperty]
        private string title = "测量数据采集";

        // 直接引用全局配置中的当前配方
        public MeasurementRecipe? CurrentRecipe => _recipeConfigService.CurrentRecipe;

        public IEnumerable<MeasurementChannel> Channels => CurrentRecipe?.Channels?.Where(c => c.IsEnabled) ?? Enumerable.Empty<MeasurementChannel>();


        [ObservableProperty]
        private bool isAcquiring;



        [ObservableProperty]
        private int passCount;

        [ObservableProperty]
        private int failCount;

        [ObservableProperty]
        private int totalCount;
        private ObservableCollection<MeasurementChannel>? _channels;
        public HomeViewModel(ILog log, IRecipeConfigService recipeConfigService, IUserSettingsService userSettingsService)
        {
            _log = log;
            _recipeConfigService = recipeConfigService;
            _userSettingsService = userSettingsService;

            // 不再从用户设置加载图片，图片跟随配方

            if (_recipeConfigService is INotifyPropertyChanged notifyPropertyChanged)
            {
                notifyPropertyChanged.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(IRecipeConfigService.CurrentRecipe))
                    {
                        // 解绑旧的
                        if (_channels != null)
                            _channels.CollectionChanged -= Channels_CollectionChanged;

                        _channels = CurrentRecipe?.Channels;

                        // 绑定新的
                        if (_channels != null)
                        {
                            _channels.CollectionChanged += Channels_CollectionChanged;
                            foreach (var ch in _channels)
                                ch.PropertyChanged += Channel_PropertyChanged;
                        }

                        OnPropertyChanged(nameof(CurrentRecipe));
                        OnPropertyChanged(nameof(Channels)); // 刷新 UI

                        // 加载配方的产品图片
                        ProductImagePath = CurrentRecipe?.ProductImagePath ?? string.Empty;

                        _log.Info($"当前配方已更新: {CurrentRecipe?.RecipeName}");
                    }
                };
            }

            // 初始化时也要绑定
            _channels = CurrentRecipe?.Channels;
            if (_channels != null)
            {
                _channels.CollectionChanged += Channels_CollectionChanged;
                foreach (var ch in _channels)
                    ch.PropertyChanged += Channel_PropertyChanged;
            }

        }
        private void Channels_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (MeasurementChannel c in e.NewItems)
                    c.PropertyChanged += Channel_PropertyChanged;
            if (e.OldItems != null)
                foreach (MeasurementChannel c in e.OldItems)
                    c.PropertyChanged -= Channel_PropertyChanged;
            OnPropertyChanged(nameof(Channels)); // 刷新 UI
        }

        private void Channel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {

            OnPropertyChanged(nameof(Channels));
        }
        [RelayCommand]
        private async Task StartAcquisitionAsync()
        {
            if (CurrentRecipe == null)
            {
                _log.Warn("未选择配方");
                return;
            }

            IsAcquiring = true;
            _log.Info("开始数据采集");

            // 模拟数据采集
            await Task.Delay(1000);

            // 模拟测量结果
            var random = new Random();
            foreach (var channel in Channels)
            {
                var offset = (random.NextDouble() - 0.5) * 0.2;
                channel.MeasuredValue = channel.StandardValue + offset;

                var upperLimit = channel.StandardValue + channel.UpperTolerance;
                var lowerLimit = channel.StandardValue - channel.LowerTolerance;

                if (channel.MeasuredValue >= lowerLimit && channel.MeasuredValue <= upperLimit)
                {
                    channel.Result = MeasurementResult.Pass;
                }
                else
                {
                    channel.Result = MeasurementResult.Fail;
                }
            }

            // 更新统计
            TotalCount++;
            var allPass = Channels.All(c => c.Result == MeasurementResult.Pass);
            if (allPass)
            {
                PassCount++;
            }
            else
            {
                FailCount++;

            }

            IsAcquiring = false;
        }

        [RelayCommand]
        private void StopAcquisition()
        {
            IsAcquiring = false;
            _log.Info("停止数据采集");
        }

        [RelayCommand]
        private void ClearData()
        {
            foreach (var channel in Channels)
            {
                channel.MeasuredValue = 0;
                channel.Result = MeasurementResult.NotMeasured;
                channel.HistoricalData.Clear();
            }

            PassCount = 0;
            FailCount = 0;
            TotalCount = 0;
            _log.Info("数据已清除");
        }

        [RelayCommand]
        private void ImportProductImage()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All Files (*.*)|*.*",
                Title = "选择产品图片"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                ProductImagePath = openFileDialog.FileName;
                // 保存到当前配方
                if (CurrentRecipe != null)
                {
                    CurrentRecipe.ProductImagePath = openFileDialog.FileName;
                    _log.Info($"已设置产品图片: {openFileDialog.FileName}");
                }
            }
        }
    }
}