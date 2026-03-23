using System.Windows;

namespace MeasurementSoftware.Services.Appearance
{
    /// <summary>
    /// 应用全局外观服务。
    /// </summary>
    public interface IAppAppearanceService
    {
        /// <summary>
        /// 当前统一字体名称。
        /// </summary>
        string CurrentFontFamily { get; }

        /// <summary>
        /// 当前统一基准字号。
        /// </summary>
        double CurrentFontSize { get; }

        /// <summary>
        /// 初始化全局字体资源。
        /// </summary>
        void Initialize();

        /// <summary>
        /// 将全局字体应用到指定根元素。
        /// </summary>
        /// <param name="element">需要应用外观的根元素。</param>
        void Attach(FrameworkElement element);

        /// <summary>
        /// 更新全局字号并实时刷新界面。
        /// </summary>
        /// <param name="fontSize">新的字号。</param>
        void UpdateFontSize(double fontSize);
    }
}
