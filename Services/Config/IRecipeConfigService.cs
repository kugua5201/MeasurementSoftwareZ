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
        void OpenRecipe(MeasurementRecipe recipe, string path);
        void CloseRecipe();
        void UpdateRecipePath(string path);
        Task<bool> SaveCurrentRecipeAsync();
        Task<MeasurementRecipe?> LoadRecipeAsync(string path);

        /// <summary>
        /// 当前是否采集中
        /// </summary>
        bool IsCollecting { get; }

        void SetCollect(bool Collect);
    }
}
