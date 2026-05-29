using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services;
using MeasurementSoftware.Services.Config;
using MeasurementSoftware.Services.Logs;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace MeasurementSoftware.ViewModels
{
    public partial class SpcViewModel : ObservableViewModel
    {
        private readonly ILogService _log;
        private readonly IRecipeConfigService _recipeConfigService;
        private readonly IDataRecordService _dataRecordService;
        private readonly ISpcService _spcService;

        public IEnumerable<MeasurementChannel> Channels =>
            _recipeConfigService.CurrentRecipe?.Channels?
                .Where(c => c.IsEnabled)
                .OrderBy(c => c.ChannelNumber)
                .ToList()
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

        [ObservableProperty]
        private int selectedChartTabIndex;

        [ObservableProperty]
        private SpcChartModel xbarChartModel = CreateEmptyChartModel("Xbar 控制图", "子组编号", "Xbar");

        [ObservableProperty]
        private SpcChartModel rChartModel = CreateEmptyChartModel("R 控制图", "子组编号", "极差 R");

        [ObservableProperty]
        private SpcChartModel histogramChartModel = CreateEmptyChartModel("分布直方图", "测量值", "频次");

        [ObservableProperty]
        private SpcChartModel trendChartModel = CreateEmptyChartModel("数据趋势图", "样本序号", "测量值");

        private string _analysisStatus = "请选择通道并加载数据";
        public string AnalysisStatus
        {
            get => _analysisStatus;
            set => SetProperty(ref _analysisStatus, value);
        }

        private string _analysisSummary = "当前未生成分析结果";
        public string AnalysisSummary
        {
            get => _analysisSummary;
            set => SetProperty(ref _analysisSummary, value);
        }

        private bool _hasAnalysisResult;
        public bool HasAnalysisResult
        {
            get => _hasAnalysisResult;
            set => SetProperty(ref _hasAnalysisResult, value);
        }

        private bool _isAnalyzing;
        public bool IsAnalyzing
        {
            get => _isAnalyzing;
            set => SetProperty(ref _isAnalyzing, value);
        }

        private ObservableCollection<MeasurementChannel>? _channels;

        public SpcViewModel(
            ILogService log,
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

            RefreshSelectedChannel();
            RefreshStatusForCurrentSelection();
        }

        private void Channels_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (MeasurementChannel c in e.NewItems)
                    c.PropertyChanged += Channel_PropertyChanged;
            if (e.OldItems != null)
                foreach (MeasurementChannel c in e.OldItems)
                    c.PropertyChanged -= Channel_PropertyChanged;

            RefreshSelectedChannel();
            OnPropertyChanged(nameof(Channels));
        }

        private void Channel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MeasurementChannel.IsEnabled)
                || e.PropertyName == nameof(MeasurementChannel.ChannelName)
                || e.PropertyName == nameof(MeasurementChannel.ChannelNumber))
            {
                RefreshSelectedChannel();
                OnPropertyChanged(nameof(Channels));
            }

            if (sender == SelectedChannel &&
                (e.PropertyName == nameof(MeasurementChannel.ChannelName)
                || e.PropertyName == nameof(MeasurementChannel.StandardValue)
                || e.PropertyName == nameof(MeasurementChannel.UpperTolerance)
                || e.PropertyName == nameof(MeasurementChannel.LowerTolerance)))
            {
                RefreshStatusForCurrentSelection();
            }
        }

        partial void OnSelectedChannelChanged(MeasurementChannel? value)
        {
            RefreshStatusForCurrentSelection();
        }

        partial void OnSubgroupSizeChanged(int value)
        {
            if (value < 2)
            {
                SubgroupSize = 2;
                return;
            }

            if (value > 10)
            {
                SubgroupSize = 10;
            }
        }

        partial void OnStartDateChanged(DateTime value)
        {
            RefreshStatusForCurrentSelection();
        }

        partial void OnEndDateChanged(DateTime value)
        {
            RefreshStatusForCurrentSelection();
        }

        private void RefreshSelectedChannel()
        {
            var enabledChannels = Channels.ToList();
            if (enabledChannels.Count == 0)
            {
                SelectedChannel = null;
                return;
            }

            if (SelectedChannel == null || !enabledChannels.Contains(SelectedChannel))
            {
                SelectedChannel = enabledChannels[0];
            }
        }

        private void RefreshStatusForCurrentSelection()
        {
            if (SelectedChannel == null)
            {
                AnalysisStatus = "当前配方没有可用通道，请先启用测量通道";
                if (!HasAnalysisResult)
                {
                    AnalysisSummary = "当前未生成分析结果";
                }

                return;
            }

            var start = StartDate.Date;
            var end = EndDate.Date.AddDays(1).AddSeconds(-1);
            AnalysisStatus = $"已选择通道：{SelectedChannel.ChannelName}，分析范围：{start:yyyy-MM-dd} ~ {end:yyyy-MM-dd}";

            if (!HasAnalysisResult)
            {
                AnalysisSummary = $"规格：中心值 {SelectedChannel.StandardValue:F4}，USL {SelectedChannel.StandardValue + SelectedChannel.UpperTolerance:F4}，LSL {SelectedChannel.StandardValue - SelectedChannel.LowerTolerance:F4}";
            }
        }

        private void ResetAnalysisResultState(bool clearHistory)
        {
            CurrentSpcResult = null;
            XbarRData = null;
            HistogramBinCenters = [];
            HistogramFrequencies = [];
            RawData.Clear();
            HasAnalysisResult = false;
            RebuildChartModels();

            if (clearHistory)
            {
                SpcResults.Clear();
            }
        }

        private void RebuildChartModels()
        {
            XbarChartModel = BuildXbarChartModel();
            RChartModel = BuildRChartModel();
            HistogramChartModel = BuildHistogramChartModel();
            TrendChartModel = BuildTrendChartModel();
        }

        private static SpcChartModel CreateEmptyChartModel(string title, string xLabel, string yLabel)
        {
            return new SpcChartModel
            {
                Title = "",
                XLabel = xLabel,
                YLabel = yLabel
            };
        }

        private SpcChartModel BuildXbarChartModel()
        {
            var model = CreateEmptyChartModel("Xbar 控制图", "子组编号", "Xbar");
            if (XbarRData == null || XbarRData.Points.Count == 0)
            {
                return model;
            }

            model.Series.Add(new SpcChartSeries
            {
                SeriesType = SpcChartSeriesType.Scatter,
                XValues = XbarRData.Points.Select(p => (double)p.SubgroupIndex).ToArray(),
                YValues = XbarRData.Points.Select(p => p.XbarValue).ToArray(),
                ColorHex = "#2196F3"
            });

            model.ReferenceLines.Add(new SpcChartReferenceLine { LineType = SpcChartReferenceLineType.Horizontal, Value = XbarRData.Limits.XbarCL, ColorHex = "#2196F3" });
            model.ReferenceLines.Add(new SpcChartReferenceLine { LineType = SpcChartReferenceLineType.Horizontal, Value = XbarRData.Limits.XbarUCL, ColorHex = "#F44336" });
            model.ReferenceLines.Add(new SpcChartReferenceLine { LineType = SpcChartReferenceLineType.Horizontal, Value = XbarRData.Limits.XbarLCL, ColorHex = "#F44336" });
            return model;
        }

        private SpcChartModel BuildRChartModel()
        {
            var model = CreateEmptyChartModel("R 控制图", "子组编号", "极差 R");
            if (XbarRData == null || XbarRData.Points.Count == 0)
            {
                return model;
            }

            model.Series.Add(new SpcChartSeries
            {
                SeriesType = SpcChartSeriesType.Scatter,
                XValues = XbarRData.Points.Select(p => (double)p.SubgroupIndex).ToArray(),
                YValues = XbarRData.Points.Select(p => p.RangeValue).ToArray(),
                ColorHex = "#2196F3"
            });

            model.ReferenceLines.Add(new SpcChartReferenceLine { LineType = SpcChartReferenceLineType.Horizontal, Value = XbarRData.Limits.RCL, ColorHex = "#2196F3" });
            model.ReferenceLines.Add(new SpcChartReferenceLine { LineType = SpcChartReferenceLineType.Horizontal, Value = XbarRData.Limits.RUCL, ColorHex = "#F44336" });

            if (XbarRData.Limits.RLCL > 0)
            {
                model.ReferenceLines.Add(new SpcChartReferenceLine { LineType = SpcChartReferenceLineType.Horizontal, Value = XbarRData.Limits.RLCL, ColorHex = "#F44336" });
            }

            return model;
        }

        private SpcChartModel BuildHistogramChartModel()
        {
            var model = CreateEmptyChartModel("分布直方图", "测量值", "频次");
            if (HistogramBinCenters.Length == 0)
            {
                return model;
            }

            model.Series.Add(new SpcChartSeries
            {
                SeriesType = SpcChartSeriesType.Bar,
                XValues = HistogramBinCenters,
                YValues = HistogramFrequencies.Select(f => (double)f).ToArray(),
                ColorHex = "#2196F3",
                Size = HistogramBinCenters.Length > 1 ? (HistogramBinCenters[1] - HistogramBinCenters[0]) * 0.9 : 1
            });

            if (CurrentSpcResult != null)
            {
                model.ReferenceLines.Add(new SpcChartReferenceLine { LineType = SpcChartReferenceLineType.Vertical, Value = CurrentSpcResult.USL, ColorHex = "#F44336" });
                model.ReferenceLines.Add(new SpcChartReferenceLine { LineType = SpcChartReferenceLineType.Vertical, Value = CurrentSpcResult.LSL, ColorHex = "#F44336" });
                model.ReferenceLines.Add(new SpcChartReferenceLine { LineType = SpcChartReferenceLineType.Vertical, Value = CurrentSpcResult.Nominal, ColorHex = "#4CAF50" });
            }

            return model;
        }

        private SpcChartModel BuildTrendChartModel()
        {
            var model = CreateEmptyChartModel("数据趋势图", "样本序号", "测量值");
            if (RawData.Count == 0)
            {
                return model;
            }

            model.Series.Add(new SpcChartSeries
            {
                SeriesType = SpcChartSeriesType.Scatter,
                XValues = Enumerable.Range(1, RawData.Count).Select(i => (double)i).ToArray(),
                YValues = RawData.ToArray(),
                ColorHex = "#2196F3"
            });

            if (CurrentSpcResult != null)
            {
                model.ReferenceLines.Add(new SpcChartReferenceLine { LineType = SpcChartReferenceLineType.Horizontal, Value = CurrentSpcResult.USL, ColorHex = "#F44336" });
                model.ReferenceLines.Add(new SpcChartReferenceLine { LineType = SpcChartReferenceLineType.Horizontal, Value = CurrentSpcResult.LSL, ColorHex = "#F44336" });
                model.ReferenceLines.Add(new SpcChartReferenceLine { LineType = SpcChartReferenceLineType.Horizontal, Value = CurrentSpcResult.Nominal, ColorHex = "#4CAF50" });
            }

            return model;
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

            var start = StartDate.Date;
            var end = EndDate.Date.AddDays(1).AddSeconds(-1);
            if (end < start)
            {
                Growl.Warning("结束日期不能早于起始日期");
                AnalysisStatus = "结束日期不能早于起始日期";
                return;
            }

            if (SubgroupSize < 2 || SubgroupSize > 10)
            {
                Growl.Warning("子组大小仅支持 2 到 10");
                return;
            }

            try
            {
                IsAnalyzing = true;
                AnalysisStatus = $"正在分析 {SelectedChannel.ChannelName} ...";
                ResetAnalysisResultState(clearHistory: false);

                var recipeName = _recipeConfigService.CurrentRecipe?.BasicInfo?.RecipeName?.Trim();
                var records = await _dataRecordService.QueryRecordsAsync(start, end, recipeName, null);

                var channelData = records
                    .OrderBy(r => r.MeasurementTime)
                    .Where(r => string.IsNullOrWhiteSpace(recipeName)
                        || string.Equals(r.RecipeName?.Trim(), recipeName, StringComparison.OrdinalIgnoreCase))
                    .SelectMany(r => r.ChannelData)
                    .Where(c => c.ChannelNumber == SelectedChannel.ChannelNumber
                        || string.Equals(c.ChannelName?.Trim(), SelectedChannel.ChannelName?.Trim(), StringComparison.OrdinalIgnoreCase))
                    .Select(c => c.MeasuredResultValue)
                    .ToList();

                if (SelectedChannel.HistoricalData.Count > 0)
                {
                    channelData.AddRange(SelectedChannel.HistoricalData);
                }

                if (channelData.Count == 0)
                {
                    AnalysisStatus = $"{SelectedChannel.ChannelName} 在所选时间范围内没有可分析数据";
                    AnalysisSummary = "请先执行测量，或调整时间范围后重试";
                    Growl.Warning($"{SelectedChannel.ChannelName} 没有数据，请先执行测量");
                    return;
                }

                RawData = new ObservableCollection<double>(channelData);

                var spcResult = _spcService.CalculateSpc(
                    SelectedChannel.ChannelName,
                    channelData,
                    SelectedChannel.StandardValue,
                    SelectedChannel.UpperTolerance,
                    SelectedChannel.LowerTolerance);

                CurrentSpcResult = spcResult;
                HasAnalysisResult = true;

                XbarRData = _spcService.GenerateXbarRChart(channelData, SubgroupSize);

                var (centers, freqs) = _spcService.GenerateHistogram(channelData);
                HistogramBinCenters = centers;
                HistogramFrequencies = freqs;
                RebuildChartModels();

                var existing = SpcResults.FirstOrDefault(r => r.ChannelName == spcResult.ChannelName);
                if (existing != null)
                    SpcResults.Remove(existing);
                SpcResults.Insert(0, spcResult);

                var subgroupCount = XbarRData?.Points.Count ?? 0;
                AnalysisStatus = $"分析完成：{SelectedChannel.ChannelName}，样本 {channelData.Count} 个，子组 {subgroupCount} 个";
                AnalysisSummary = $"均值 {spcResult.Mean:F4}，标准差 {spcResult.StdDev:F6}，Cpk {spcResult.Cpk:F3}，合格率 {spcResult.YieldRate:F1}%";

                Growl.Info($"分析完成: {channelData.Count} 个样本, Cpk={spcResult.Cpk:F3} ({spcResult.CpkLevel})");
                _log.Info($"SPC分析: 配方={recipeName}, 通道={SelectedChannel.ChannelName}, 时间范围={start:yyyy-MM-dd HH:mm:ss}~{end:yyyy-MM-dd HH:mm:ss}, 样本={channelData.Count}, Cpk={spcResult.Cpk:F3}");
            }
            catch (Exception ex)
            {
                ResetAnalysisResultState(clearHistory: false);
                AnalysisStatus = "SPC分析失败";
                AnalysisSummary = ex.Message;
                _log.Error($"SPC分析失败: {ex.Message}");
                Growl.Error($"SPC分析失败: {ex.Message}");
            }
            finally
            {
                IsAnalyzing = false;
            }
        }

        /// <summary>
        /// 清除分析结果
        /// </summary>
        [RelayCommand]
        private void ClearResults()
        {
            ResetAnalysisResultState(clearHistory: true);
            RefreshStatusForCurrentSelection();
            Growl.Warning("已清除当前分析结果");
        }
    }
}
