namespace MeasurementSoftware.Models
{
    /// <summary>
    /// 用户设置（保存应用级别的状态）
    /// </summary>
    public class UserSettings
    {
        /// <summary>
        /// 上次打开的配方文件路径
        /// </summary>
        public string LastRecipePath { get; set; } = string.Empty;

        /// <summary>
        /// 主导航布局
        /// </summary>
        public MainNavigationLayoutSettings MainNavigationLayout { get; set; } = new();

        /// <summary>
        /// 上次更新时间
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        /// <summary>
        /// 主窗口布局设置
        /// </summary>
        public WindowLayoutSettings WindowLayout { get; set; } = new();

        /// <summary>
        /// 测量页面布局设置
        /// </summary>
        public HomeLayoutSettings HomeLayout { get; set; } = new();

        /// <summary>
        /// 通道设置页面布局设置
        /// </summary>
        public ChannelSettingLayoutSettings ChannelSettingLayout { get; set; } = new();

        /// <summary>
        /// 应用外观设置
        /// </summary>
        public AppAppearanceSettings Appearance { get; set; } = new();
    }

    /// <summary>
    /// 应用外观设置
    /// </summary>
    public class AppAppearanceSettings
    {
        /// <summary>
        /// 统一字体名称
        /// </summary>
        public string FontFamily { get; set; } = "Microsoft YaHei UI";

        /// <summary>
        /// 基准字体大小
        /// </summary>
        public double FontSize { get; set; } = 14;
    }

    /// <summary>
    /// 主窗口布局设置
    /// </summary>
    public class WindowLayoutSettings
    {
        public double Left { get; set; } = double.NaN;
        public double Top { get; set; } = double.NaN;
        public double Width { get; set; } = 1000;
        public double Height { get; set; } = 600;
        public bool IsMaximized { get; set; }
        public double MenuColumnWidth { get; set; } = 250;
    }

    /// <summary>
    /// 主导航布局
    /// </summary>
    public class MainNavigationLayoutSettings
    {
        /// <summary>
        /// 已打开页面
        /// </summary>
        public List<string> OpenPages { get; set; } = ["Home"];

        /// <summary>
        /// 当前选中页面
        /// </summary>
        public string SelectedPage { get; set; } = "Home";
    }

    /// <summary>
    /// 测量页面布局设置
    /// </summary>
    public class HomeLayoutSettings
    {
        /// <summary>
        /// 是否为备选布局（垂直布局）
        /// </summary>
        public bool IsAlternateLayout { get; set; }

        /// <summary>
        /// 导向区是否可见
        /// </summary>
        public bool IsGuidePanelVisible { get; set; } = true;

        /// <summary>
        /// 默认布局 - 右侧导向区列宽
        /// </summary>
        public double GuideColumnWidth { get; set; } = 220;

        /// <summary>
        /// 默认布局 - 底部表格行高比例（Star值）
        /// </summary>
        public double TableRowStarHeight { get; set; } = 0.6;

        /// <summary>
        /// 备选布局 - 右侧列宽比例（Star值）
        /// </summary>
        public double AltRightColumnStarWidth { get; set; } = 1.2;

        /// <summary>
        /// 备选布局 - 导向区行高
        /// </summary>
        public double AltGuideRowHeight { get; set; } = 220;
    }

    /// <summary>
    /// 通道设置页面布局设置
    /// </summary>
    public class ChannelSettingLayoutSettings
    {
        /// <summary>
        /// 是否为垂直布局
        /// </summary>
        public bool IsVertical { get; set; } = true;

        /// <summary>
        /// 水平布局 - 右列宽度比例（Star值）
        /// </summary>
        public double RightColumnStarWidth { get; set; } = 1.2;

        /// <summary>
        /// 垂直布局 - 下方行高比例（Star值）
        /// </summary>
        public double BottomRowStarHeight { get; set; } = 1.2;
    }
}
