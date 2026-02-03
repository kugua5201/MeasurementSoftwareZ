using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services
{
    /// <summary>
    /// 配方配置服务接口
    /// </summary>
    public interface IRecipeConfigService
    {
        /// <summary>
        /// 当前打开的配方（只读）
        /// </summary>
        MeasurementRecipe? CurrentRecipe { get; }

        /// <summary>
        /// 当前配方的文件路径（只读）
        /// </summary>
        string CurrentRecipePath { get; }

        /// <summary>
        /// 打开配方
        /// </summary>
        void OpenRecipe(MeasurementRecipe recipe, string path);

        /// <summary>
        /// 关闭当前配方
        /// </summary>
        void CloseRecipe();

        /// <summary>
        /// 更新配方路径（另存为时使用）
        /// </summary>
        void UpdateRecipePath(string path);

        /// <summary>
        /// 保存当前配方
        /// </summary>
        Task<bool> SaveCurrentRecipeAsync();

        /// <summary>
        /// 加载配方
        /// </summary>
        Task<MeasurementRecipe?> LoadRecipeAsync(string path);
    }
}
