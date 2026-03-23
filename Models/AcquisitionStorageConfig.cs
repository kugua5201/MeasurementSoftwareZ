using MeasurementSoftware.ViewModels;
using System.Collections.ObjectModel;

namespace MeasurementSoftware.Models
{
    /// <summary>
    /// 采集结果文件存储配置。
    /// 统一管理自动/手动存储、命名规则、目录与单文件大小限制。
    /// </summary>
    public class AcquisitionStorageConfig : ObservableViewModel
    {
        private bool autoSaveEnabled = true;
        private string fileNamePattern = "{RecipeName}";
        private string outputFolder = string.Empty;
        private int maxFileSizeMb = 10;
        private ObservableCollection<AcquisitionCsvColumnConfig> csvColumns = AcquisitionCsvColumnCatalog.CreateDefaultSelection();

        /// <summary>
        /// 是否启用存储。
        /// </summary>
        public bool AutoSaveEnabled
        {
            get => autoSaveEnabled;
            set => SetProperty(ref autoSaveEnabled, value);
        }

        /// <summary>
        /// 文件名规则。
        /// 默认建议仅使用稳定名称持续追加，例如 {RecipeName}。
        /// 支持 {RecipeName}、{RecipeId}、{Time:yyyyMMdd_HHmmss} 等占位符。
        /// </summary>
        public string FileNamePattern
        {
            get => fileNamePattern;
            set => SetProperty(ref fileNamePattern, value);
        }

        /// <summary>
        /// 存储目录。
        /// 为空时默认使用软件目录下的 AcquisitionRecords 文件夹。
        /// </summary>
        public string OutputFolder
        {
            get => outputFolder;
            set => SetProperty(ref outputFolder, value);
        }

        /// <summary>
        /// 单个存储文件最大大小（MB）。
        /// 超过后自动生成递增编号的新文件。
        /// </summary>
        public int MaxFileSizeMb
        {
            get => maxFileSizeMb;
            set => SetProperty(ref maxFileSizeMb, value <= 0 ? 10 : value);
        }

        /// <summary>
        /// CSV导出列配置。
        /// 以可持久化的列对象形式保存，便于界面维护与后续可靠解析导入。
        /// </summary>
        public ObservableCollection<AcquisitionCsvColumnConfig> CsvColumns
        {
            get => csvColumns;
            set => SetProperty(ref csvColumns, value ?? []);
        }
    }
}
