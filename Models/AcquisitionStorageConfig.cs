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
        private string fileNamePattern = string.Empty;
        private string fileBaseName = "测量记录";
        private bool appendRecipeNameToFileName = true;
        private bool appendDateToFileName = true;
        private bool appendTimeToFileName;
        private bool appendBarcodeToFileName;
        private string outputFolder = string.Empty;
        private int maxFileSizeMb = 10;
        private bool exportSingleLinePerMeasurement = true;
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
        /// 兼容旧版本的文件名规则字段。
        /// 新界面不再直接编辑该字段，仅用于兼容历史配方配置。
        /// </summary>
        public string FileNamePattern
        {
            get => fileNamePattern;
            set => SetProperty(ref fileNamePattern, value);
        }

        /// <summary>
        /// 文件基础名称。
        /// 由用户直接输入纯文本名称，不再要求手工输入占位符。
        /// </summary>
        public string FileBaseName
        {
            get => fileBaseName;
            set => SetProperty(ref fileBaseName, value.Trim());
        }

        /// <summary>
        /// 是否在文件名后附加配方名称。
        /// </summary>
        public bool AppendRecipeNameToFileName
        {
            get => appendRecipeNameToFileName;
            set => SetProperty(ref appendRecipeNameToFileName, value);
        }

        /// <summary>
        /// 是否在文件名后附加日期。
        /// </summary>
        public bool AppendDateToFileName
        {
            get => appendDateToFileName;
            set => SetProperty(ref appendDateToFileName, value);
        }

        /// <summary>
        /// 是否在文件名后附加时间。
        /// </summary>
        public bool AppendTimeToFileName
        {
            get => appendTimeToFileName;
            set => SetProperty(ref appendTimeToFileName, value);
        }

        /// <summary>
        /// 是否在文件名后附加二维码。
        /// </summary>
        public bool AppendBarcodeToFileName
        {
            get => appendBarcodeToFileName;
            set => SetProperty(ref appendBarcodeToFileName, value);
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
        /// 是否按单行展开整次测量导出。
        /// 启用后每次测量导出一行，前面为总字段，后面按通道展开对应列。
        /// </summary>
        public bool ExportSingleLinePerMeasurement
        {
            get => exportSingleLinePerMeasurement;
            set => SetProperty(ref exportSingleLinePerMeasurement, value);
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
