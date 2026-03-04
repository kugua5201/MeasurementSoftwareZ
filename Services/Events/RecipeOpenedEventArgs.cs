using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services.Events
{
    /// <summary>
    /// 配方打开事件
    /// </summary>
    public class RecipeOpenedEventArgs
    {
        public MeasurementRecipe Recipe { get; set; }

        public RecipeOpenedEventArgs(MeasurementRecipe recipe)
        {
            Recipe = recipe;
        }
    }
}
