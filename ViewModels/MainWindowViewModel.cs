using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using MeasurementSoftware.Extensions;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Licensing;
using MeasurementSoftware.Services.Logs;
using MeasurementSoftware.Services.UserSetting;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MeasurementSoftware.ViewModels
{
    public partial class MainWindowViewModel : ObservableViewModel
    {
        [ObservableProperty]
        private string title = "SF-GAMS";

        [ObservableProperty]
        private TabItemModel? _selectedTab;

        [ObservableProperty]
        private ObservableCollection<TabItemModel> _tabs = new();

        private bool _isAppLoading;

        public bool IsAppLoading
        {
            get => _isAppLoading;
            set => SetProperty(ref _isAppLoading, value);
        }

        private string _appLoadingMessage = "正在启动并连接设备，请稍候...";

        public string AppLoadingMessage
        {
            get => _appLoadingMessage;
            set => SetProperty(ref _appLoadingMessage, value);
        }

        private readonly ILogService _log;
        private readonly IUserSettingsService _userSettingsService;


        public MainWindowViewModel(ILogService log, IUserSettingsService userSettingsService)
        {
            _log = log;
            _userSettingsService = userSettingsService;
            RestoreNavigationLayout();
        }



        /// <summary>
        /// 关闭标签页命令
        /// </summary>
        [RelayCommand]
        private void CloseTab(TabItemModel tab)
        {
            if (tab != null && Tabs.Contains(tab))
            {
                Tabs.Remove(tab);
            }
        }

        /// <summary>
        /// 根据页面名称导航到指定页面
        /// </summary>
        /// <param name="pageName">页面名称</param>
        [RelayCommand]
        private void NavigateToPage(string pageName)
        {
            var friendlyName = GetFriendlyName(pageName);
            var icon = GetIcon(pageName);

            // 检查是否已存在该标签页
            var existingTab = Tabs.FirstOrDefault(t => t.Header == friendlyName);
            if (existingTab != null)
            {
                SelectedTab = existingTab;
                return;
            }

            var page = Navigation.GetPage(pageName);  // 直接使用Navigation静态类
            if (page != null)
            {
                var newTab = new TabItemModel
                {
                    Header = friendlyName,
                    Content = page,
                    IsClosable = pageName != "Home",
                    Icon = GetIcon(pageName),
                    OpenTabs = Tabs
                };
                Tabs.Add(newTab);
                SelectedTab = newTab;
            }
            else
            {
                _log.Error($"导航失败: 未找到页面 '{pageName}'");
            }
        }

        private void RestoreNavigationLayout()
        {
            var layout = _userSettingsService.Settings.MainNavigationLayout;
            var openPages = layout.OpenPages
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct()
                .ToList();

            if (openPages.Count == 0)
            {
                openPages.Add("Home");
            }

            foreach (var page in openPages)
            {
                NavigateToPage(page);
            }

            var selectedPage = string.IsNullOrWhiteSpace(layout.SelectedPage)
                ? "Home"
                : layout.SelectedPage;

            NavigateToPage(selectedPage);

            if (Tabs.Count == 0)
            {
                NavigateToPage("Home");
            }
        }

        public void SaveNavigationLayout()
        {
            var layout = _userSettingsService.Settings.MainNavigationLayout;
            layout.OpenPages = [.. Tabs
                .Select(t => GetPageNameByHeader(t.Header))
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct()];

            if (layout.OpenPages.Count == 0)
            {
                layout.OpenPages = ["Home"];
            }

            layout.SelectedPage = GetPageNameByHeader(SelectedTab?.Header);
            if (string.IsNullOrWhiteSpace(layout.SelectedPage))
            {
                layout.SelectedPage = layout.OpenPages[0];
            }

            _userSettingsService.SaveSettings();
        }

        public void SetAppLoading(bool isLoading, string? message = null)
        {
            IsAppLoading = isLoading;
            if (!string.IsNullOrWhiteSpace(message))
            {
                AppLoadingMessage = message;
            }
        }

        private string GetPageNameByHeader(string? header)
        {
            return header switch
            {
                "测量" => "Home",
                "配方管理" => "RecipeManagement",
                "校准" => "Calibration",
                "检测记录" => "DataManagement",
                "SPC分析" => "Spc",
                "通道配置" => "ChannelSetting",
                "设备管理" => "CommunicationSetting",
                "参数监控" => "WriteDataPoint",
                "参数监控配置" => "WriteDataPointConfig",
                "二维码配置" => "QrCodeSetting",
                "其他设置" => "OtherSettings",
                "日志" => "LogViewer",
                "置零" => "Zeroing",
                _ => string.Empty
            };
        }

        private string GetFriendlyName(string pageName)
        {
            return pageName switch
            {
                "Home" => "测量",
                "RecipeManagement" => "配方管理",
                "Calibration" => "校准",
                "Zeroing" => "置零",
                "DataManagement" => "检测记录",
                "DataRecord" => "检测记录",
                "ChannelSetting" => "通道配置",
                "DeviceManagement" => "设备管理",
                "CommunicationSetting" => "设备管理",
                "WriteDataPoint" => "参数监控",
                "WriteDataPointConfig" => "参数监控配置",
                "QrCodeSetting" => "二维码配置",
                "MesSetting" => "MES配置",
                "Spc" => "SPC分析",
                "OtherSettings" => "其他设置",
                "LogViewer" => "日志",
                _ => pageName
            };
        }

        private string GetIcon(string pageName)
        {
            return pageName switch
            {
                "Home" => "\xE80F",
                "RecipeManagement" => "\xE7C3",
                "Calibration" => "\xE9E9",
                "Zeroing" => "\xE65A",
                "DataManagement" => "\xE8F1",
                "DataRecord" => "\xE8F1",
                "ChannelSetting" => "\xE762",
                "DeviceManagement" => "\xE772",
                "CommunicationSetting" => "\xE772",
                "WriteDataPoint" => "\xE895",
                "WriteDataPointConfig" => "\xE895",
                "QrCodeSetting" => "\xE8A7",
                "MesSetting" => "\xE774",
                "Spc" => "\xE9D9",
                "OtherSettings" => "\xE713",
                "Setting" => "\xE713",
                "LogViewer" => "\xE7BA",
                _ => "\xE80F"
            };
        }



        /// <summary>
        /// 导航到配方管理页面
        /// </summary>
        [RelayCommand]
        private void NavigateToRecipeManagement() => NavigateToPage("RecipeManagement");

        /// <summary>
        /// 导航到通信设置页面
        /// </summary>
        [RelayCommand]
        private void NavigateToCommunicationSetting() => NavigateToPage("CommunicationSetting");

        /// <summary>
        /// 导航到通道设置页面
        /// </summary>
        [RelayCommand]
        private void NavigateToChannelSetting() => NavigateToPage("ChannelSetting");

        /// <summary>
        /// 导航到写入点位页面
        /// </summary>
        [RelayCommand]
        private void NavigateToWriteDataPoint() => NavigateToPage("WriteDataPoint");

        /// <summary>
        /// 导航到写入点位配置页面
        /// </summary>
        [RelayCommand]
        private void NavigateToWriteDataPointConfig() => NavigateToPage("WriteDataPointConfig");

        /// <summary>
        /// 导航到首页（测量页面）
        /// </summary>
        [RelayCommand]
        private void NavigateToHome() => NavigateToPage("Home");

        /// <summary>
        /// 导航到校准页面
        /// </summary>
        [RelayCommand]
        private void NavigateToCalibration() => NavigateToPage("Calibration");

        [RelayCommand]
        private void NavigateToZeroing() => NavigateToPage("Zeroing");

        /// <summary>
        /// 导航到检测记录页面
        /// </summary>
        [RelayCommand]
        private void NavigateToDataManagement() => NavigateToPage("DataManagement");

        /// <summary>
        /// 导航到数据记录页面（兼容旧名称）
        /// </summary>
        [RelayCommand]
        private void NavigateToDataRecord() => NavigateToPage("DataManagement");

        /// <summary>
        /// 导航到条码配置页面
        /// </summary>
        [RelayCommand]
        private void NavigateToBarcodeSetting() => NavigateToPage("BarcodeSetting");

        /// <summary>
        /// 导航到二维码配置页面
        /// </summary>
        [RelayCommand]
        private void NavigateToQrCodeSetting() => NavigateToPage("QrCodeSetting");

        /// <summary>
        /// 导航到MES配置页面
        /// </summary>
        [RelayCommand]
        private void NavigateToMesSetting()
        {
            Growl.Warning("MES系统对接功能正在开发中，敬请期待！");

            //NavigateToPage("MesSetting");
        }

        /// <summary>
        /// 导航到日志查看页面
        /// </summary>
        [RelayCommand]
        private void NavigateToLogViewer() => NavigateToPage("LogViewer");

        /// <summary>
        /// 导航到SPC分析页面
        /// </summary>
        [RelayCommand]
        private void NavigateToSpc() => NavigateToPage("Spc");

        /// <summary>
        /// 导航到其他设置页面
        /// </summary>
        [RelayCommand]
        private void NavigateToOtherSettings() => NavigateToPage("OtherSettings");
    }
}
