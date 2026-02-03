using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services.Recipe
{
    /// <summary>
    /// 配方服务接口
    /// </summary>
    public interface IRecipeService
    {
        /// <summary>
        /// 获取所有配方
        /// </summary>
        Task<List<MeasurementRecipe>> GetAllRecipesAsync();

        /// <summary>
        /// 获取配方
        /// </summary>
        Task<MeasurementRecipe?> GetRecipeByIdAsync(string recipeId);

        /// <summary>
        /// 保存配方
        /// </summary>
        Task<bool> SaveRecipeAsync(MeasurementRecipe recipe);

        /// <summary>
        /// 删除配方
        /// </summary>
        Task<bool> DeleteRecipeAsync(string recipeId);

        /// <summary>
        /// 获取默认配方
        /// </summary>
        Task<MeasurementRecipe?> GetDefaultRecipeAsync();

        /// <summary>
        /// 从文件加载配方
        /// </summary>
        Task<MeasurementRecipe?> LoadRecipeFromFileAsync(string filePath);

        /// <summary>
        /// 保存配方到指定文件
        /// </summary>
        Task<bool> SaveRecipeToFileAsync(MeasurementRecipe recipe, string filePath);
    }
}
