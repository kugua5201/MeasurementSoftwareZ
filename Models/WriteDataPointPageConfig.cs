using MeasurementSoftware.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeasurementSoftware.Models
{
    /// <summary>
    /// 写入点位页签配置。
    /// 仅保存页签名称和顺序，避免改动写入点位主结构。
    /// </summary>
    public class WriteDataPointPageConfig : ObservableViewModel
    {
        private int pageIndex = 1;
        private string pageName = "页面1";
        private string editingPageName = "页面1";
        private int order;
        private bool isEditing;

        /// <summary>
        /// 页码。
        /// </summary>
        public int PageIndex
        {
            get => pageIndex;
            set => SetProperty(ref pageIndex, Math.Max(1, value));
        }

        /// <summary>
        /// 页签名称。
        /// </summary>
        public string PageName
        {
            get => pageName;
            set => SetProperty(ref pageName, string.IsNullOrWhiteSpace(value) ? $"页面{PageIndex}" : value.Trim(), () => EditingPageName = pageName);
        }

        /// <summary>
        /// 编辑中的页签名称。
        /// 用于在编辑取消时恢复原始名称。
        /// </summary>
        [JsonIgnore]
        public string EditingPageName
        {
            get => editingPageName;
            set => SetProperty(ref editingPageName, value);
        }

        /// <summary>
        /// 页签顺序。
        /// </summary>
        public int Order
        {
            get => order;
            set => SetProperty(ref order, Math.Max(0, value));
        }

        [JsonIgnore]
        public bool IsEditing
        {
            get => isEditing;
            set => SetProperty(ref isEditing, value);
        }

        public override string ToString()
        {
            return PageName;
        }
    }

}
