namespace MeasurementSoftware.Services.Events
{
    /// <summary>
    /// 配方保存事件
    /// </summary>
    public class RecipeSavedEvent
    {
        public string RecipeId { get; set; } = string.Empty;
        public string RecipeName { get; set; } = string.Empty;
    }
}
