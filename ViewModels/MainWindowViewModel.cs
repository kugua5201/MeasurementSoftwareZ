using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeasurementSoftware.Extensions;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Logs;
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
        private string title = "demo";

        [ObservableProperty]
        private TabItemModel? _selectedTab;

        [ObservableProperty]
        private ObservableCollection<TabItemModel> _tabs = new();

        private readonly ILog _log;
        public MainWindowViewModel(ILog log)
        {
            _log = log;
            NavigateToPage("Home");
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

        private string GetFriendlyName(string pageName)
        {
            return pageName switch
            {
                "Home" => "首页",
                "About" => "关于",
                "ChannelSetting" => "通道设置",
                "DeviceManagement" => "设备管理",
                "CommunicationSetting" => "设备管理", // 兼容旧名称
                "LogViewer" => "日志",
                _ => pageName
            };
        }

        private string GetIcon(string pageName)
        {
            return pageName switch
            {
                "Home" => "\xE80F",
                "About" => "\xE946",
                "ChannelSetting" => "\xE762",
                "DeviceManagement" => "\xE968",
                "CommunicationSetting" => "\xE968",
                "Setting" => "\xE713",
                "LogViewer" => "\xE7BA",
                _ => "\xE80F"
            };
        }



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
        /// 导航到关于页面
        /// </summary>
        [RelayCommand]
        private void NavigateToAbout() => NavigateToPage("About");

        /// <summary>
        /// 导航到首页
        /// </summary>
        [RelayCommand]
        private void NavigateToHome() => NavigateToPage("Home");

        /// <summary>
        /// 导航到日志查看页面
        /// </summary>
        [RelayCommand]
        private void NavigateToLogViewer() => NavigateToPage("LogViewer");
    }
}
