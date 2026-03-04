using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services;
using MeasurementSoftware.Services.Config;
using MeasurementSoftware.Services.Logs;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MeasurementSoftware.ViewModels
{
    public partial class SpcViewModel : ObservableViewModel
    {
        private readonly ILog _log;
        private readonly IRecipeConfigService _recipeConfigService;
        private readonly IDataRecordService _dataRecordService;
        private readonly ISpcService _spcService;

        public IEnumerable<MeasurementChannel> Channels =>
            _recipeConfigService.CurrentRecipe?.Channels?.Where(c => c.IsEnabled)
            ?? [];

        [ObservableProperty]
        private MeasurementChannel? selectedChannel;

        [ObservableProperty]
        private DateTime startDate = DateTime.Now.AddDays(-30);

        [ObservableProperty]
        private DateTime endDate = DateTime.Now;

        [ObservableProperty]
        private SpcResult? currentSpcResult;

        [ObservableProperty]
        private XbarRChartData? xbarRData;

        [ObservableProperty]
        private double[] histogramBinCenters = [];

        [ObservableProperty]
        private int[] histogramFrequencies = [];

        //[ObservableProperty]
        //private string analysisStatus = "请选择通道并加载数据";

        /// <summary>
        /// 原始数据列表（供图表和表格展示）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<double> rawData = [];

        /// <summary>
        /// SPC分析结果历史（可对比多通道）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<SpcResult> spcResults = [];

        /// <summary>
        /// 子组大小（Xbar-R 控制图）
        /// </summary>
        [ObservableProperty]
        private int subgroupSize = 5;

        private ObservableCollection<MeasurementChannel>? _channels;

        public SpcViewModel(
            ILog log,
            IRecipeConfigService recipeConfigService,
            IDataRecordService dataRecordService,
            ISpcService spcService)
        {
            _log = log;
            _recipeConfigService = recipeConfigService;
            _dataRecordService = dataRecordService;
            _spcService = spcService;

            if (_recipeConfigService is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(IRecipeConfigService.CurrentRecipe))
                    {
                        BindChannels();
                        OnPropertyChanged(nameof(Channels));
                    }
                };
            }

            BindChannels();
        }

        private void BindChannels()
        {
            if (_channels != null)
            {
                _channels.CollectionChanged -= Channels_CollectionChanged;
                foreach (var ch in _channels)
                    ch.PropertyChanged -= Channel_PropertyChanged;
            }

            _channels = _recipeConfigService.CurrentRecipe?.Channels;

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
            OnPropertyChanged(nameof(Channels));
        }

        private void Channel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MeasurementChannel.IsEnabled))
                OnPropertyChanged(nameof(Channels));
        }

        /// <summary>
        /// 加载数据并执行SPC分析
        /// </summary>
        [RelayCommand]
        private async Task AnalyzeAsync()
        {
            if (SelectedChannel == null)
            {
                Growl.Warning("请先选择通道");
                return;
            }


            // 从记录服务查询历史数据
            var records = await _dataRecordService.QueryRecordsAsync(StartDate, EndDate);

            // 提取当前通道的测量值
            var channelData = records
                .SelectMany(r => r.ChannelData)
                .Where(c => c.ChannelNumber == SelectedChannel.ChannelNumber)
                .Select(c => c.MeasuredValue)
                .ToList();

            // 同时合并通道的实时历史数据
            if (SelectedChannel.HistoricalData.Count > 0)
            {
                channelData.AddRange(SelectedChannel.HistoricalData);
            }

            if (channelData.Count == 0)
            {
                Growl.Warning($"{SelectedChannel.ChannelName} 没有数据，请先执行测量");
                return;
            }

            RawData = new ObservableCollection<double>(channelData);

            // 执行SPC计算
            var spcResult = _spcService.CalculateSpc(
                SelectedChannel.ChannelName,
                channelData,
                SelectedChannel.StandardValue,
                SelectedChannel.UpperTolerance,
                SelectedChannel.LowerTolerance);

            CurrentSpcResult = spcResult;

            // 生成Xbar-R控制图
            XbarRData = _spcService.GenerateXbarRChart(channelData, SubgroupSize);

            // 生成直方图
            var (centers, freqs) = _spcService.GenerateHistogram(channelData);
            HistogramBinCenters = centers;
            HistogramFrequencies = freqs;

            // 更新结果列表
            var existing = SpcResults.FirstOrDefault(r => r.ChannelName == spcResult.ChannelName);
            if (existing != null)
                SpcResults.Remove(existing);
            SpcResults.Insert(0, spcResult);

            Growl.Info($"分析完成: {channelData.Count} 个样本, Cpk={spcResult.Cpk:F3} ({spcResult.CpkLevel})");
            _log.Info($"SPC分析: {SelectedChannel.ChannelName}, 样本={channelData.Count}, Cpk={spcResult.Cpk:F3}");
        }

        /// <summary>
        /// 清除分析结果
        /// </summary>
        [RelayCommand]
        private void ClearResults()
        {
            CurrentSpcResult = null;
            XbarRData = null;
            HistogramBinCenters = [];
            HistogramFrequencies = [];
            RawData.Clear();
            SpcResults.Clear();
            Growl.Warning("请选择通道并加载数据");
        }
    }
}
