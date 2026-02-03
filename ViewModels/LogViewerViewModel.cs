using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Logs;
using System.Collections.ObjectModel;
using System.IO;

namespace MeasurementSoftware.ViewModels
{
    public partial class LogViewerViewModel : ObservableViewModel
    {
        private readonly ILog _log;
        private List<string> _allLogLines = new(); // 存储所有日志行（字符串，轻量级）
        private List<string> _filteredLogLines = new(); // 过滤后的日志行

        [ObservableProperty]
        private ObservableCollection<LogEntry> displayedLogs = new();

        [ObservableProperty]
        private int currentPage = 1;

        partial void OnCurrentPageChanged(int value)
        {
            _ = LoadCurrentPageAsync();
        }

        [ObservableProperty]
        private int pageSize = 100;

        [ObservableProperty]
        private int totalPages = 1;

        [ObservableProperty]
        private string selectedLevel = "全部";

        [ObservableProperty]
        private DateTime selectedDate = DateTime.Today;

        [ObservableProperty]
        private string statusMessage = "就绪";

        public string[] LogLevels { get; } = { "全部", "DEBUG", "INFO", "WARN", "ERROR" };

        public LogViewerViewModel(ILog log)
        {
            _log = log;
            _ = LoadLogLinesAsync();
        }

        partial void OnSelectedDateChanged(DateTime value)
        {
            CurrentPage = 1;
            _ = LoadLogLinesAsync();
        }

        partial void OnSelectedLevelChanged(string value)
        {
            CurrentPage = 1;
            ApplyFilterAndLoadPage();
        }

        /// <summary>
        /// 第一步：扫描并加载所有日志行（只存字符串，内存占用小）
        /// </summary>
        [RelayCommand]
        private async Task LoadLogLinesAsync()
        {
            try
            {
                StatusMessage = "正在扫描日志文件...";
                _allLogLines.Clear();

                var logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                if (!Directory.Exists(logFolder))
                {
                    StatusMessage = "日志目录不存在";
                    DisplayedLogs.Clear();
                    return;
                }

                var dateStr = SelectedDate.ToString("yyyy-MM-dd");
                var logSubFolders = new[] { "Info", "Warn", "Error", "Debug", "Trace" };

                // 倒序加载，最新的在前
                foreach (var subFolder in logSubFolders)
                {
                    var folderPath = Path.Combine(logFolder, subFolder);
                    if (!Directory.Exists(folderPath)) continue;

                    var logFiles = Directory.GetFiles(folderPath, $"*{dateStr}*.txt").OrderByDescending(f => File.GetLastWriteTime(f)); // 最新文件在前

                    foreach (var file in logFiles)
                    {
                        try
                        {
                            var lines = await File.ReadAllLinesAsync(file);
                            // 倒序添加，最新的行在前
                            _allLogLines.AddRange(lines.Reverse());
                        }
                        catch
                        {
                            // 跳过无法读取的文件
                        }
                    }
                }

                StatusMessage = $"已扫描 {_allLogLines.Count} 条日志";
                ApplyFilterAndLoadPage();
            }
            catch (Exception ex)
            {
                StatusMessage = $"加载失败: {ex.Message}";
                _log.Error($"加载日志失败: {ex.Message}");
                DisplayedLogs.Clear();
            }
        }

        /// <summary>
        /// 第二步：应用级别过滤
        /// </summary>
        private void ApplyFilterAndLoadPage()
        {
            // 按级别过滤
            if (SelectedLevel == "全部")
            {
                _filteredLogLines = _allLogLines;
            }
            else
            {
                _filteredLogLines = _allLogLines.Where(line => line.Contains($"| {SelectedLevel} |")).ToList();
            }

            TotalPages = (int)Math.Ceiling(_filteredLogLines.Count / (double)PageSize);
            if (TotalPages == 0) TotalPages = 1;

            // 确保页码有效
            var pageIndex = Math.Max(1, Math.Min(CurrentPage, TotalPages));
            if (CurrentPage != pageIndex)
            {
                CurrentPage = pageIndex;
                return; // OnCurrentPageChanged 会再次调用
            }

            _ = LoadCurrentPageAsync();
        }

        /// <summary>
        /// 第三步：只解析当前页需要的日志（懒加载）
        /// </summary>
        private async Task LoadCurrentPageAsync()
        {
            await Task.Run(() =>
            {
                var skip = (CurrentPage - 1) * PageSize;
                var pageLines = _filteredLogLines.Skip(skip).Take(PageSize).ToList();

                var pageEntries = new List<LogEntry>();
                var idCounter = skip + 1; // ID 从 1 开始，最新的是 1

                foreach (var line in pageLines)
                {
                    if (TryParseLogLine(line, out var entry))
                    {
                        entry.Id = idCounter++;
                        pageEntries.Add(entry);
                    }
                }

                // 更新 UI
                App.Current.Dispatcher.Invoke(() =>
                {
                    DisplayedLogs = new ObservableCollection<LogEntry>(pageEntries);
                    StatusMessage = $"第 {CurrentPage}/{TotalPages} 页，共 {_filteredLogLines.Count} 条日志";
                });
            });
        }

        private bool TryParseLogLine(string line, out LogEntry entry)
        {
            entry = new LogEntry();
            try
            {
                // NLog 格式: 2024-01-15 10:30:45.1234 | 0 | INFO | MeasurementSoftware.xxx | Message
                var parts = line.Split('|');
                if (parts.Length < 4) return false;

                var timestampStr = parts[0].Trim();
                entry.Timestamp = DateTime.Parse(timestampStr);
                entry.Level = parts[2].Trim();
                entry.Source = parts[3].Trim();
                entry.Message = parts.Length > 4 ? string.Join("|", parts.Skip(4)).Trim() : "";
                return true;
            }
            catch
            {
                return false;
            }
        }

        [RelayCommand]
        private void ClearLogs()
        {
            _allLogLines.Clear();
            _filteredLogLines.Clear();
            DisplayedLogs.Clear();
            TotalPages = 1;
            CurrentPage = 1;
            StatusMessage = "日志已清空";
            _log.Info("日志已清空");
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadLogLinesAsync();
        }
    }
}
