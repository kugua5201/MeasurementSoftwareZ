using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services.Config
{
    /// <summary>
    /// 配方配置服务接口
    /// </summary>
    public interface IRecipeConfigService
    {
        MeasurementRecipe? CurrentRecipe { get; }
        string CurrentRecipePath { get; }
        string LastSaveErrorMessage { get; }
        void OpenRecipe(MeasurementRecipe recipe, string path);
        void CloseRecipe();
        void UpdateRecipePath(string path);
        Task<bool> SaveCurrentRecipeAsync();
        Task<MeasurementRecipe?> LoadRecipeAsync(string path);

        /// <summary>
        /// 当前是否采集中
        /// </summary>
        bool IsCollecting { get; }

        /// <summary>
        /// 采集轮询延迟(ms)
        /// </summary>
        int AcquisitionDelayMs { get; set; }

        /// <summary>
        /// 确保配方统计信息已初始化。
        /// 用于兼容旧配方缺少统计节点的情况。
        /// </summary>
        void EnsureRecipeStatistics(MeasurementRecipe? recipe);

        /// <summary>
        /// 重置指定配方的统计信息。
        /// </summary>
        void ResetRecipeStatistics(MeasurementRecipe? recipe);

        void SetCollect(bool Collect);
    }
}
