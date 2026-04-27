using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using MeasurementSoftware.Extensions;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services;
using MeasurementSoftware.Services.Logs;
using Microsoft.Win32;
using System.Collections.ObjectModel;

namespace MeasurementSoftware.ViewModels
{
    public partial class DataRecordViewModel : ObservableViewModel
    {
        private readonly List<MeasurementRecord> _allRecords = [];
        private readonly ILogService _log;
        private readonly IDataRecordService _dataRecordService;

        [ObservableProperty]
        private ObservableCollection<MeasurementRecord> records = new();

        [ObservableProperty]
        private MeasurementRecord? selectedRecord;

        [ObservableProperty]
        private DateTime startDate = DateTime.Now.AddMonths(-1);

        [ObservableProperty]
        private DateTime endDate = DateTime.Now;

        [ObservableProperty]
        private string searchKeyword = string.Empty;

        [ObservableProperty]
        private int currentPage = 1;

        [ObservableProperty]
        private int pageSize = 20;

        [ObservableProperty]
        private bool isChannelDetailDrawerOpen;

        [ObservableProperty]
        private string channelDetailTitle = "通道检测详情";

        [ObservableProperty]
        private string channelDetailSummaryText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<ChannelMeasurementData> channelDetailItems = [];

        public int TotalCount => _allRecords.Count;

        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)Math.Max(1, PageSize)));

        public string PageSummary => $"第 {CurrentPage}/{TotalPages} 页，共 {TotalCount} 条";

        public DataRecordViewModel(ILogService log, IDataRecordService dataRecordService)
        {
            _log = log;
            _dataRecordService = dataRecordService;
        }

        [RelayCommand]
        private async Task Query()
        {
            try
            {
                var start = StartDate.Date;
                var end = EndDate.Date.AddDays(1).AddSeconds(-1);
                var keyword = SearchKeyword.Trim();

                SelectedRecord = null;
                var results = await _dataRecordService.QueryRecordsAsync(start, end, keyword, keyword);
                _log.Info($"查询: {start:yyyy-MM-dd HH:mm:ss} 至 {end:yyyy-MM-dd HH:mm:ss}，关键字={keyword}");
                _allRecords.Clear();
                _allRecords.AddRange(results);

                if (CurrentPage != 1)
                {
                    CurrentPage = 1;
                }
                else
                {
                    RefreshPagedRecords();
                }

                if (results.Count == 0)
                {
                    Growl.Warning("未查询到匹配的数据");
                }
            }
            catch (Exception ex)
            {
                _log.Error($"查询失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ExportCsv()
        {
            if (_allRecords.Count == 0)
            {
                Growl.Warning("没有数据可导出");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "CSV文件|*.csv",
                FileName = $"检测记录_{DateTime.Now:yyyyMMddHHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                var success = await _dataRecordService.ExportToCsvAsync(_allRecords.ToList(), dialog.FileName);
                if (success)
                {
                    Growl.Info($"导出CSV成功: {dialog.FileName}");
                }
            }
        }

        [RelayCommand]
        private void OpenChannelDetail(MeasurementRecord? record)
        {
            var targetRecord = record ?? SelectedRecord;
            if (targetRecord == null)
            {
                Growl.Warning("请先选择一条记录");
                return;
            }

            if (targetRecord.ChannelData.Count == 0)
            {
                Growl.Warning("当前记录没有通道检测数据");
                return;
            }

            ChannelDetailTitle = $"通道检测详情 - {targetRecord.RecipeName}";
            ChannelDetailSummaryText = $"配方：{targetRecord.RecipeName}    时间：{targetRecord.MeasurementTime:yyyy-MM-dd HH:mm:ss}    操作员：{targetRecord.OperatorName}    二维码：{targetRecord.Barcode}    通道数：{targetRecord.ChannelData.Count}";
            var channelItems = targetRecord.ChannelData
                .OrderBy(c => c.ChannelNumber)
                .ToList();

            for (var i = 0; i < channelItems.Count; i++)
            {
                channelItems[i].DisplayIndex = i + 1;
            }

            ChannelDetailItems = new ObservableCollection<ChannelMeasurementData>(channelItems);
            IsChannelDetailDrawerOpen = true;
        }

        [RelayCommand]
        private void CloseChannelDetailDrawer()
        {
            IsChannelDetailDrawerOpen = false;
        }

        partial void OnCurrentPageChanged(int value)
        {
            if (value <= 0)
            {
                if (CurrentPage != 1)
                {
                    CurrentPage = 1;
                }

                return;
            }

            if (_allRecords.Count > 0)
            {
                RefreshPagedRecords();
            }
            else
            {
                OnPropertyChanged(nameof(PageSummary));
            }
        }

        partial void OnPageSizeChanged(int value)
        {
            if (value <= 0)
            {
                PageSize = 20;
                return;
            }

            if (CurrentPage != 1)
            {
                CurrentPage = 1;
            }
            else
            {
                RefreshPagedRecords();
            }
        }

        private void RefreshPagedRecords()
        {
            if (_allRecords.Count == 0)
            {
                Records = [];
                OnPropertyChanged(nameof(TotalCount));
                OnPropertyChanged(nameof(TotalPages));
                OnPropertyChanged(nameof(PageSummary));
                return;
            }

            var totalPages = TotalPages;
            if (CurrentPage > totalPages)
            {
                CurrentPage = totalPages;
            }

            var pageItems = _allRecords
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            for (var i = 0; i < pageItems.Count; i++)
            {
                pageItems[i].DisplayIndex = (CurrentPage - 1) * PageSize + i + 1;
            }

            Records = new ObservableCollection<MeasurementRecord>(pageItems);
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(PageSummary));
        }
    }
}
