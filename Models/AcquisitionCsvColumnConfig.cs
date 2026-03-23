using MeasurementSoftware.ViewModels;
using System.Collections.ObjectModel;

namespace MeasurementSoftware.Models
{
    /// <summary>
    /// 采集导出列配置。
    /// Key 用于可靠解析，Header 用于界面显示与导出列头。
    /// </summary>
    public class AcquisitionCsvColumnConfig : ObservableViewModel
    {
        private string key = string.Empty;
        private string header = string.Empty;

        public string Key
        {
            get => key;
            set => SetProperty(ref key, value);
        }

        public string Header
        {
            get => header;
            set => SetProperty(ref header, value);
        }
    }

    /// <summary>
    /// 可选采集导出列定义。
    /// </summary>
    public sealed record AcquisitionCsvColumnDefinition(string Key, string Header);

    /// <summary>
    /// 采集导出列定义目录。
    /// 统一管理可选列，便于界面添加与导出解析保持一致。
    /// </summary>
    public static class AcquisitionCsvColumnCatalog
    {
        public static IReadOnlyList<AcquisitionCsvColumnDefinition> All { get; } =
        [
            new("RecipeName", "配方名称"),
            new("MeasurementTime", "测量时间"),
            new("IsStepMeasurement", "是否工步测量"),
            new("CurrentStepNumber", "当前工步编号"),
            new("TotalSteps", "总工步数"),
            new("OverallResult", "总结果"),
            new("ChannelNumber", "通道编号"),
            new("ChannelName", "通道名称"),
            new("ChannelDescription", "通道说明"),
            new("ChannelStepNumber", "通道工步编号"),
            new("ChannelStepName", "通道工步名称"),
            new("ChannelType", "通道类型"),
            new("DataSourceAddress", "数据源地址"),
            new("PlcDeviceName", "PLC设备"),
            new("DataPointName", "数据点名称"),
            new("IsEnabled", "是否启用"),
            new("StandardValue", "标准值"),
            new("UpperTolerance", "公差上限"),
            new("LowerTolerance", "公差下限"),
            new("MeasuredValue", "测量值"),
            new("Unit", "单位"),
            new("DecimalPlaces", "小数位数"),
            new("RequiresCalibration", "是否校准"),
            new("CalibrationMode", "校准方式"),
            new("CalibrationCoefficientA", "校准系数A"),
            new("CalibrationCoefficientB", "校准系数B"),
            new("UseCacheValue", "是否使用缓存"),
            new("SampleCount", "采样数量"),
            new("ChannelResult", "通道结果")
        ];

        public static ObservableCollection<AcquisitionCsvColumnConfig> CreateDefaultSelection()
        {
            return new ObservableCollection<AcquisitionCsvColumnConfig>(All.Select(ToConfig));
        }

        public static AcquisitionCsvColumnConfig ToConfig(AcquisitionCsvColumnDefinition definition)
        {
            return new AcquisitionCsvColumnConfig
            {
                Key = definition.Key,
                Header = definition.Header
            };
        }
    }
}
