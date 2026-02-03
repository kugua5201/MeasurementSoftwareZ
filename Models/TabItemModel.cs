using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeasurementSoftware.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;

namespace MeasurementSoftware.Models
{


    public partial class TabItemModel : ObservableObject
    {
        [ObservableProperty]
        private object? content;
        public ObservableCollection<TabItemModel>? OpenTabs { get; set; }

        [ObservableProperty]
        private string? header;

        [ObservableProperty]
        private string? icon = string.Empty;

        [ObservableProperty]
        private bool isClosable = true;

        public bool HasClosableLeft => OpenTabs != null && OpenTabs.Take(OpenTabs.IndexOf(this)).Any(t => t.IsClosable);

        public bool HasClosableRight => OpenTabs != null && OpenTabs.Skip(OpenTabs.IndexOf(this) + 1).Any(t => t.IsClosable);

        public bool HasClosableOther => OpenTabs != null && OpenTabs.Where(t => t != this).Any(t => t.IsClosable);

        public bool HasClosableAny => OpenTabs != null && OpenTabs.Any(t => t.IsClosable);

        public TabItemModel()
        {
            RefreshTabCommand = new RelayCommand(RefreshTab);
            CloseTabCommand = new RelayCommand(CloseTab);
            CloseLeftTabsCommand = new RelayCommand(CloseLeftTabs);
            CloseRightTabsCommand = new RelayCommand(CloseRightTabs);
            CloseOtherTabsCommand = new RelayCommand(CloseOtherTabs);
            CloseAllTabsCommand = new RelayCommand(CloseAllTabs);
        }

        public IRelayCommand RefreshTabCommand { get; }
        public IRelayCommand CloseTabCommand { get; }
        public IRelayCommand CloseLeftTabsCommand { get; }
        public IRelayCommand CloseRightTabsCommand { get; }
        public IRelayCommand CloseOtherTabsCommand { get; }
        public IRelayCommand CloseAllTabsCommand { get; }

        private void RefreshTab()
        {
            // 这里可用 Messenger 或事件聚合器
            // Messenger.Send(new TabRefreshMessage(this));
        }

        private void CloseTab()
        {
            if (!IsClosable || OpenTabs?.Any() != true)
                return;
            OpenTabs.Remove(this);
        }

        private void CloseLeftTabs()
        {
            if (OpenTabs?.Any() != true)
                return;
            int currentIndex = OpenTabs.IndexOf(this);
            if (currentIndex <= 0)
                return;
            var leftTabs = OpenTabs.Take(currentIndex).Where(t => t.IsClosable).ToList();
            foreach (var tab in leftTabs)
                tab.CloseTab();
        }

        private void CloseRightTabs()
        {
            if (OpenTabs?.Any() != true)
                return;
            int currentIndex = OpenTabs.IndexOf(this);
            if (currentIndex < 0 || currentIndex >= OpenTabs.Count - 1)
                return;
            var rightTabs = OpenTabs.Skip(currentIndex + 1).Where(t => t.IsClosable).ToList();
            foreach (var tab in rightTabs)
                tab.CloseTab();
        }

        private void CloseOtherTabs()
        {
            if (OpenTabs?.Any() != true)
                return;
            var otherTabs = OpenTabs.Where(t => t != this && t.IsClosable).ToList();
            foreach (var tab in otherTabs)
                tab.CloseTab();
        }

        private void CloseAllTabs()
        {
            if (OpenTabs?.Any() != true)
                return;
            var allClosableTabs = OpenTabs.Where(t => t.IsClosable).ToList();
            foreach (var tab in allClosableTabs)
                tab.CloseTab();
        }
    }
}
