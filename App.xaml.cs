using Autofac;
using Autofac.Core;
using MeasurementSoftware.Extensions;
using MeasurementSoftware.Services;
using MeasurementSoftware.Services.Events;
using MeasurementSoftware.Services.Logs;
using MeasurementSoftware.Services.Recipe;
using MeasurementSoftware.UserControls;
using MeasurementSoftware.ViewModels;
using MultiProtocol.Services.IIndustrialProtocol;
using System.IO;
using System.Windows;

namespace MeasurementSoftware
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IContainer? _container;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var builder = new ContainerBuilder();

            // 注册服务
            builder.RegisterSingleton<ILog, Log>();
            builder.RegisterSingleton<IRecipeService, RecipeService>();
            builder.RegisterSingleton<IEventAggregator, EventAggregator>();
            builder.RegisterSingleton<IUserSettingsService, UserSettingsService>();
       
            // 注册 AppConfig（全局唯一实例，实现两个接口）
            builder.RegisterType<AppConfig>()
                .AsSelf()
                .As<IDeviceConfigService>()
                .As<IRecipeConfigService>()
                .SingleInstance();

            // 注册主窗口
            builder.RegisterMainWindow<MainWindow, MainWindowViewModel>();

            // 注册页面 (View + ViewModel)
            builder.RegisterViewWithViewModel<HomeUserControl, HomeViewModel>("Home");
            builder.RegisterViewWithViewModel<ChannelSettingUserControl, ChannelSettingViewModel>("ChannelSetting");
            builder.RegisterViewWithViewModel<AboutUserControl, AboutViewModel>("About");
            builder.RegisterViewWithViewModel<CommunicationSettingUserControl, CommunicationSettingViewModel>("DeviceManagement");
            builder.RegisterViewWithViewModel<CommunicationSettingUserControl, CommunicationSettingViewModel>("CommunicationSetting"); // 兼容旧名称
            builder.RegisterViewWithViewModel<SettingUserControl, SettingViewModel>("Setting");
            builder.RegisterViewWithViewModel<LogViewerUserControl, LogViewerViewModel>("LogViewer");

            // 构建容器（包含所有服务、窗口和页面）
            _container = builder.Build();

            // 设置全局容器引用
            ContainerBuilderExtensions.SetContainer(_container);

            // 启动时加载全局配置
            _ = InitializeAppConfigAsync();

            // 显示主窗口
            var mainWindow = _container.Resolve<MainWindow>();
            mainWindow.Show();
        }

        private async Task InitializeAppConfigAsync()
        {
            try
            {
                var log = _container!.Resolve<ILog>();
                var appConfig = _container!.Resolve<AppConfig>();
                var userSettings = _container!.Resolve<IUserSettingsService>();
                var recipeService = _container!.Resolve<IRecipeService>();

                log.Info("正在加载应用程序配置...");

                // 1. 加载用户设置
                await userSettings.LoadSettingsAsync();

                // 2. 加载 PLC 设备配置
                await appConfig.LoadDevicesAsync();

                // 3. 自动加载上次打开的配方
                if (!string.IsNullOrEmpty(userSettings.Settings.LastRecipePath) &&
                    File.Exists(userSettings.Settings.LastRecipePath))
                {
                    var recipe = await recipeService.LoadRecipeFromFileAsync(userSettings.Settings.LastRecipePath);
                    if (recipe != null)
                    {
                        appConfig.OpenRecipe(recipe, userSettings.Settings.LastRecipePath);
                        log.Info($"已自动加载上次打开的配方: {recipe.RecipeName}");
                    }
                }

                log.Info("应用程序配置加载完成");
            }
            catch (Exception ex)
            {
                var log = _container?.Resolve<ILog>();
                log?.Error($"加载配置失败: {ex.Message}");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 退出时保存配置
            var userSettings = _container?.Resolve<IUserSettingsService>();
            userSettings?.SaveSettingsAsync();
            base.OnExit(e);
        }
    }
}