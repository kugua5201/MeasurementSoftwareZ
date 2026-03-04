using CommunityToolkit.Mvvm.ComponentModel;
using MeasurementSoftware.ViewModels;

namespace MeasurementSoftware.Models
{
    /// <summary>
    /// MES上传配置
    /// </summary>
    public partial class MesUploadConfig : ObservableViewModel
    {
        /// <summary>
        /// 是否启用MES上传
        /// </summary>
        [ObservableProperty]
        private bool isEnabled;

        /// <summary>
        /// MES数据库类型
        /// </summary>
        [ObservableProperty]
        private MesDatabaseType databaseType = MesDatabaseType.SqlServer;

        /// <summary>
        /// 服务器地址
        /// </summary>
        [ObservableProperty]
        private string serverAddress = "localhost";

        /// <summary>
        /// 端口号
        /// </summary>
        [ObservableProperty]
        private int port = 1433;

        /// <summary>
        /// 数据库名称
        /// </summary>
        [ObservableProperty]
        private string databaseName = "MES";

        /// <summary>
        /// 用户名
        /// </summary>
        [ObservableProperty]
        private string username = "sa";

        /// <summary>
        /// 密码
        /// </summary>
        [ObservableProperty]
        private string password = string.Empty;

        /// <summary>
        /// 表名称
        /// </summary>
        [ObservableProperty]
        private string tableName = "MeasurementRecords";

        /// <summary>
        /// 是否自动上传
        /// </summary>
        [ObservableProperty]
        private bool autoUpload = true;

        /// <summary>
        /// 上传失败是否重试
        /// </summary>
        [ObservableProperty]
        private bool retryOnFailure = true;

        /// <summary>
        /// 重试次数
        /// </summary>
        [ObservableProperty]
        private int maxRetryCount = 3;

        /// <summary>
        /// 重试间隔（秒）
        /// </summary>
        [ObservableProperty]
        private int retryInterval = 5;

        /// <summary>
        /// 连接超时（秒）
        /// </summary>
        [ObservableProperty]
        private int connectionTimeout = 30;
    }

    /// <summary>
    /// MES数据库类型
    /// </summary>
    public enum MesDatabaseType
    {
        SqlServer,
        MySQL,
        PostgreSQL,
        Oracle
    }
}
