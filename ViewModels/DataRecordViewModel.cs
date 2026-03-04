using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services;
using MeasurementSoftware.Services.Logs;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;

namespace MeasurementSoftware.ViewModels
{
    public partial class DataRecordViewModel : ObservableViewModel
    {
        private readonly ILog _log;
        private readonly IDataRecordService _dataRecordService;

        [ObservableProperty]
        private ObservableCollection<MeasurementRecord> records = new();

        [ObservableProperty]
        private MeasurementRecord? selectedRecord;

        [ObservableProperty]
        private DateTime startDate = DateTime.Now.AddDays(-7);

        [ObservableProperty]
        private DateTime endDate = DateTime.Now;

        [ObservableProperty]
        private Visibility codeTextbox = Visibility.Collapsed;

        private string selectedQueryMode = "按日期范围";

        public string SelectedQueryMode
        {
            get => selectedQueryMode;
            set
            {
                if (SetProperty(ref selectedQueryMode, value))
                {
                    CodeTextbox = value == "按二维码" ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        public ObservableCollection<string> QueryModes { get; } = new()
        {
            "按日期范围",
            "按年",
            "按月",
            "按日",
            "按二维码"
        };

        public DataRecordViewModel(ILog log, IDataRecordService dataRecordService)
        {
            _log = log;
            _dataRecordService = dataRecordService;
        }

        [RelayCommand]
        private async Task Query()
        {
            try
            {
                var results = await _dataRecordService.QueryRecordsAsync(StartDate, EndDate);
                _log.Info($"查询: {StartDate:yyyy-MM-dd} 至 {EndDate:yyyy-MM-dd}");
                Records = new ObservableCollection<MeasurementRecord>(results);
            }
            catch (Exception ex)
            {
                _log.Error($"查询失败: {ex.Message}");
            }
        }


        [RelayCommand]
        private void ViewDetails()
        {
            if (SelectedRecord == null) return;
            _log.Info($"查看记录详情: {SelectedRecord.RecordId}");
        }

        [RelayCommand]
        private async Task DeleteRecord()
        {
            if (SelectedRecord == null) return;
            var success = await _dataRecordService.DeleteRecordAsync(SelectedRecord.RecordId);
            if (success)
            {
                Records.Remove(SelectedRecord);
                _log.Info($"删除记录: {SelectedRecord.RecordId}");
            }
        }

        [RelayCommand]
        private async Task ExportCsv()
        {
            if (Records.Count == 0)
            {
                _log.Warn("没有数据可导出");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "CSV文件|*.csv",
                FileName = $"MeasurementData_{DateTime.Now:yyyyMMddHHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                var success = await _dataRecordService.ExportToCsvAsync(Records.ToList(), dialog.FileName);
                if (success)
                {
                    _log.Info($"导出CSV成功: {dialog.FileName}");
                }
            }
        }
    }
}
