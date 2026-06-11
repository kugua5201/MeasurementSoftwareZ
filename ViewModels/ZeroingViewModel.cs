using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Config;
using MeasurementSoftware.Services.Logs;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace MeasurementSoftware.ViewModels
{
    public partial class ZeroingViewModel : ObservableViewModel
    {
        private readonly ILogService _log;
        private readonly IRecipeConfigService _recipeConfigService;
        private ObservableCollection<MeasurementChannel>? _observedChannels;

        public ObservableCollection<MeasurementChannel> ChannelItems { get; } = new();

        [ObservableProperty]
        private MeasurementChannel? selectedChannelItem;

        public MeasurementRecipe? CurrentRecipe => _recipeConfigService.CurrentRecipe;

        public ZeroingViewModel(ILogService log, IRecipeConfigService recipeConfigService)
        {
            _log = log;
            _recipeConfigService = recipeConfigService;

            if (_recipeConfigService is INotifyPropertyChanged notifyPropertyChanged)
            {
                notifyPropertyChanged.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(IRecipeConfigService.CurrentRecipe))
                    {
                        RebindChannels();
                    }
                };
            }

            RebindChannels();
        }

        private void RebindChannels()
        {
            if (_observedChannels != null)
            {
                _observedChannels.CollectionChanged -= Channels_CollectionChanged;
                foreach (var channel in _observedChannels)
                {
                    channel.PropertyChanged -= Channel_PropertyChanged;
                }
            }

            ChannelItems.Clear();

            _observedChannels = CurrentRecipe?.Channels;
            if (_observedChannels == null)
            {
                return;
            }

            _observedChannels.CollectionChanged += Channels_CollectionChanged;
            foreach (var channel in _observedChannels)
            {
                channel.PropertyChanged += Channel_PropertyChanged;
                if (channel.IsEnabled)
                {
                    ChannelItems.Add(channel);
                }
            }
        }

        private void Channels_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (MeasurementChannel channel in e.OldItems)
                {
                    channel.PropertyChanged -= Channel_PropertyChanged;
                    var item = ChannelItems.FirstOrDefault(entry => ReferenceEquals(entry, channel));
                    if (item != null)
                    {
                        ChannelItems.Remove(item);
                    }
                }
            }

            if (e.NewItems != null)
            {
                foreach (MeasurementChannel channel in e.NewItems)
                {
                    channel.PropertyChanged += Channel_PropertyChanged;
                    if (channel.IsEnabled)
                    {
                        ChannelItems.Add(channel);
                    }
                }
            }
        }

        private void Channel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not MeasurementChannel channel || e.PropertyName != nameof(MeasurementChannel.IsEnabled))
            {
                return;
            }

            var existing = ChannelItems.FirstOrDefault(item => ReferenceEquals(item, channel));
            if (channel.IsEnabled && existing == null)
            {
                ChannelItems.Add(channel);
            }
            else if (!channel.IsEnabled && existing != null)
            {
                ChannelItems.Remove(existing);
            }
        }

        [RelayCommand]
        private void SelectAllChannels()
        {
            foreach (var item in ChannelItems)
            {
                item.IsZeroingSelected = true;
            }
        }

        [RelayCommand]
        private void ClearSelection()
        {
            foreach (var item in ChannelItems)
            {
                item.IsZeroingSelected = false;
            }
        }

        [RelayCommand]
        private void ApplySelectedZero()
        {
            //请先启动采集
            if (!_recipeConfigService.IsCollecting)
            {
                Growl.Warning("请先启动采集测量");
                return;
            }
            var selectedItems = ChannelItems.Where(item => item.IsZeroingSelected).ToList();
            if (selectedItems.Count == 0)
            {
                Growl.Warning("请先勾选需要置零的通道");
                return;
            }

            int successCount = 0;
            foreach (var item in selectedItems)
            {
                bool success = item.UsePresetZeroOffsetValue
                    ? item.ApplySpecifiedZeroOffset(item.PresetZeroOffsetValue)
                    : item.ApplyCurrentValueAsZeroOffset();

                if (success)
                {
                    successCount++;
                }
            }

            if (successCount == 0)
            {
                Growl.Warning("所选通道置零失败，请检查是否已有采样值或预设值配置");
                return;
            }

            Growl.Success($"已完成 {successCount} 个通道置零");
            _log.Info($"批量置零完成，成功 {successCount} 个通道");
        }

        [RelayCommand]
        private void ClearSelectedZero()
        {
            var selectedItems = ChannelItems.Where(item => item.IsZeroingSelected).ToList();
            if (selectedItems.Count == 0)
            {
                Growl.Warning("请先勾选需要清零偏移的通道");
                return;
            }

            foreach (var item in selectedItems)
            {
                item.ClearZeroOffset();
            }

            Growl.Success($"已清除 {selectedItems.Count} 个通道的置零偏移");
        }

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
            {
                Growl.Success("置零配置已保存");
            }
            else
            {
                Growl.Warning("置零配置保存失败");
            }
        }
    }

}
