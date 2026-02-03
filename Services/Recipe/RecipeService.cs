using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Logs;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace MeasurementSoftware.Services.Recipe
{
    /// <summary>
    /// 配方服务实现
    /// </summary>
    public class RecipeService : IRecipeService
    {
        private readonly ILog _log;
        private readonly string _recipeFolderPath;
        private readonly List<MeasurementRecipe> _recipes = new();

        public RecipeService(ILog log)
        {
            _log = log;
            _recipeFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Recipes");

            if (!Directory.Exists(_recipeFolderPath))
            {
                Directory.CreateDirectory(_recipeFolderPath);
            }

            LoadRecipes();
        }

        private void LoadRecipes()
        {
            try
            {
                var files = Directory.GetFiles(_recipeFolderPath, "*.json");
                foreach (var file in files)
                {
                    var json = File.ReadAllText(file);
                    var recipe = JsonSerializer.Deserialize<MeasurementRecipe>(json);
                    if (recipe != null)
                    {
                        _recipes.Add(recipe);
                    }
                }

                _log.Info($"已加载 {_recipes.Count} 个配方");
            }
            catch (Exception ex)
            {
                _log.Error($"加载配方失败: {ex.Message}");
            }
        }

        public Task<List<MeasurementRecipe>> GetAllRecipesAsync()
        {
            return Task.FromResult(_recipes.ToList());
        }

        public Task<MeasurementRecipe?> GetRecipeByIdAsync(string recipeId)
        {
            var recipe = _recipes.FirstOrDefault(r => r.RecipeId == recipeId);
            return Task.FromResult(recipe);
        }

        public async Task<bool> SaveRecipeAsync(MeasurementRecipe recipe)
        {
            try
            {
                recipe.ModifyTime = DateTime.Now;

                var existingRecipe = _recipes.FirstOrDefault(r => r.RecipeId == recipe.RecipeId);
                if (existingRecipe != null)
                {
                    _recipes.Remove(existingRecipe);
                }

                _recipes.Add(recipe);

                var filePath = Path.Combine(_recipeFolderPath, $"{recipe.RecipeId}.json");
                var json = JsonSerializer.Serialize(recipe, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, json);

                _log.Info($"配方已保存: {recipe.RecipeName}");
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"保存配方失败: {ex.Message}");
                return false;
            }
        }

        public Task<bool> DeleteRecipeAsync(string recipeId)
        {
            try
            {
                var recipe = _recipes.FirstOrDefault(r => r.RecipeId == recipeId);
                if (recipe != null)
                {
                    _recipes.Remove(recipe);

                    var filePath = Path.Combine(_recipeFolderPath, $"{recipeId}.json");
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }

                    _log.Info($"配方已删除: {recipe.RecipeName}");
                    return Task.FromResult(true);
                }

                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _log.Error($"删除配方失败: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        public Task<MeasurementRecipe?> GetDefaultRecipeAsync()
        {
            var defaultRecipe = _recipes.FirstOrDefault(r => r.IsDefault);
            return Task.FromResult(defaultRecipe);
        }

        public async Task<MeasurementRecipe?> LoadRecipeFromFileAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _log.Error($"配方文件不存在: {filePath}");
                    return null;
                }

                var json = await File.ReadAllTextAsync(filePath);
                var recipe = JsonSerializer.Deserialize<MeasurementRecipe>(json);

                if (recipe != null)
                {
                    _log.Info($"从文件加载配方成功: {recipe.RecipeName}");
                }

                return recipe;
            }
            catch (Exception ex)
            {
                _log.Error($"从文件加载配方失败: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> SaveRecipeToFileAsync(MeasurementRecipe recipe, string filePath)
        {
            try
            {
                recipe.ModifyTime = DateTime.Now;

                var json = JsonSerializer.Serialize(recipe, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, json);

                _log.Info($"配方已保存到文件: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"保存配方到文件失败: {ex.Message}");
                return false;
            }
        }
    }
}
