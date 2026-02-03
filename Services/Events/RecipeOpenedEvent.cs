using MeasurementSoftware.Models;

namespace MeasurementSoftware.Services.Events
{
    /// <summary>
    /// 配方打开事件
    /// </summary>
    public class RecipeOpenedEvent
    {
        public MeasurementRecipe Recipe { get; set; }

        public RecipeOpenedEvent(MeasurementRecipe recipe)
        {
            Recipe = recipe;
        }
    }
}
