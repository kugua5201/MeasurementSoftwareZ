using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Config;
using MeasurementSoftware.Services.Logs;
using System.ComponentModel;

namespace MeasurementSoftware.ViewModels
{
    public partial class OtherSettingsViewModel : ObservableViewModel
    {
        private readonly ILog _log;
        private readonly IRecipeConfigService _recipeConfigService;

        public MeasurementRecipe? CurrentRecipe => _recipeConfigService.CurrentRecipe;
        public bool HasRecipe => CurrentRecipe != null;

        public OtherSettingsViewModel(ILog log, IRecipeConfigService recipeConfigService)
        {
            _log = log;
            _recipeConfigService = recipeConfigService;

            if (_recipeConfigService is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(IRecipeConfigService.CurrentRecipe))
                    {
                        OnPropertyChanged(nameof(CurrentRecipe));
                        OnPropertyChanged(nameof(HasRecipe));
                    }
                };
            }
        }

        [RelayCommand]
        private async Task SaveRecipeAsync()
        {
            if (CurrentRecipe == null)
            {
                Growl.Warning("没有配方需要保存");
                return;
            }

            CurrentRecipe.ModifyTime = DateTime.Now;
            var success = await _recipeConfigService.SaveCurrentRecipeAsync();
            if (success)
                Growl.Success("配方保存成功");
            else
                Growl.Error("配方保存失败");
        }
    }
}
