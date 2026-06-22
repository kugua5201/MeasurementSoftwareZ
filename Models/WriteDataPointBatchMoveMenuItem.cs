using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeasurementSoftware.Models
{
    /// <summary>
    /// 批量移动通道菜单项。
    /// </summary>
    public class WriteDataPointBatchMoveMenuItem
    {
        public string Header { get; set; } = string.Empty;

        public bool IsEnabledChannel { get; set; }

        public WriteValueEditorMode? EditorMode { get; set; }

        public WriteValueLabelDisplayMode? LabelDisplayMode { get; set; }

        public WriteValueButtonInteractionMode? ButtonInteractionMode { get; set; }

        public WriteDataPointPageConfig? TargetPage { get; set; }

        public List<WriteDataPointBatchMoveMenuItem> Children { get; set; } = [];

        public bool IsLeaf => TargetPage != null;

        public bool HasChildren => Children.Count > 0;
    }
}
