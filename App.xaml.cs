using Autofac;
using Autofac.Core;
using MeasurementSoftware.Extensions;
using MeasurementSoftware.Interceptors;
using MeasurementSoftware.Models;
using MeasurementSoftware.Services;
using MeasurementSoftware.Services.Config;
using MeasurementSoftware.Services.Devices;
using MeasurementSoftware.Services.Events;
using MeasurementSoftware.Services.Logs;
using MeasurementSoftware.Services.UserSetting;
using MeasurementSoftware.UserControls;
using MeasurementSoftware.ViewModels;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace MeasurementSoftware
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public IContainer Container => _container!;
        private IContainer? _container;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var builder = new ContainerBuilder();

            builder.RegisterType<AcquiringInterceptor>();
            // 注册服务
            builder.RegisterSingleton<ILog, Log>();
            builder.RegisterSingleton<IEventAggregator, EventAggregator>();
            builder.RegisterSingleton<IUserSettingsService, UserSettingsService>();

            builder.RegisterSingleton<IMesService, MesService>();
            builder.RegisterSingleton<IDataRecordService, DataRecordService>();
            builder.RegisterSingleton<ICalibrationService, CalibrationService>();
            builder.RegisterSingleton<ISpcService, SpcService>();
            builder.RegisterSingleton<IPlcDeviceRuntimeFactory, PlcDeviceRuntimeFactory>();
            builder.RegisterSingleton<IPlcDeviceRuntimeService, PlcDeviceRuntimeService>();

            // 注册 AppConfig（全局唯一实例，实现所有配置接口）
            builder.RegisterType<AppConfig>()
                .AsSelf()
                .As<IRecipeConfigService>()
                .As<IDeviceConfigService>()
                .As<IQrCodeConfigService>()
                .SingleInstance();

            // 注册主窗口
            builder.RegisterMainWindow<MainWindow, MainWindowViewModel>();

            // 注册页面 (View + ViewModel)
            builder.RegisterViewWithViewModel<HomeUserControl, HomeViewModel>("Home");
            builder.RegisterViewWithInterceptedViewModel<RecipeManagementUserControl, RecipeManagementViewModel, AcquiringInterceptor>("RecipeManagement");
            builder.RegisterViewWithViewModel<ChannelSettingUserControl, ChannelSettingViewModel>("ChannelSetting");
            builder.RegisterViewWithViewModel<CalibrationUserControl, CalibrationViewModel>("Calibration");
            builder.RegisterViewWithViewModel<DataRecordUserControl, DataRecordViewModel>("DataManagement");
            builder.RegisterViewWithViewModel<DataRecordUserControl, DataRecordViewModel>("DataRecord");
            builder.RegisterViewWithViewModel<DeviceSettingUserControl, DeviceSettingViewModel>("CommunicationSetting");
            builder.RegisterViewWithViewModel<LogViewerUserControl, LogViewerViewModel>("LogViewer");
            builder.RegisterViewWithViewModel<QrCodeSettingUserControl, QrCodeSettingViewModel>("QrCodeSetting");
            builder.RegisterViewWithViewModel<SpcUserControl, SpcViewModel>("Spc");
            builder.RegisterViewWithViewModel<OtherSettingsUserControl, OtherSettingsViewModel>("OtherSettings");
            // TODO: 添加条码配置和MES配置页面
            // builder.RegisterViewWithViewModel<BarcodeSettingUserControl, BarcodeSettingViewModel>("BarcodeSetting");
            // builder.RegisterViewWithViewModel<MesSettingUserControl, MesSettingViewModel>("MesSetting");

            // 构建容器（包含所有服务、窗口和页面）
            _container = builder.Build();

            // 设置全局容器引用
            ContainerBuilderExtensions.SetContainer(_container);

            var userSettings = _container.Resolve<IUserSettingsService>();
            userSettings.LoadSettings();

            // 显示主窗口
            var mainWindow = _container.Resolve<MainWindow>();
            if (mainWindow.DataContext is MainWindowViewModel viewModel)
            {
                viewModel.SetAppLoading(true, "正在启动并连接设备，请稍候...");
            }
            mainWindow.Show();

            // 启动时加载全局配置
            _ = InitializeAppConfigAsync();
        }

        private async Task InitializeAppConfigAsync()
        {
            var mainWindowViewModel = _container?.Resolve<MainWindowViewModel>();
            try
            {
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

                var log = _container!.Resolve<ILog>();
                var appConfig = _container!.Resolve<AppConfig>();
                var userSettings = _container!.Resolve<IUserSettingsService>();
                var dataRecordService = _container!.Resolve<IDataRecordService>();

                log.Info("正在加载应用程序配置...");

                // 1. 初始化本地数据库
                await dataRecordService.InitializeAsync();

                // 2. 自动加载上次打开的配方（包含设备、二维码等所有配置）
                if (!string.IsNullOrEmpty(userSettings.Settings.LastRecipePath) &&
                    File.Exists(userSettings.Settings.LastRecipePath))
                {
                    var recipe = await appConfig.LoadRecipeAsync(userSettings.Settings.LastRecipePath);
                    if (recipe != null)
                    {
                        appConfig.OpenRecipe(recipe, userSettings.Settings.LastRecipePath);

                        // 初始化配方中的PLC设备连接
                        await appConfig.LoadDevicesAsync();

                        log.Info($"已自动加载配方: {recipe.BasicInfo.RecipeName}（含 {appConfig.Devices.Count} 个设备）");
                    }
                }

                log.Info("应用程序配置加载完成");
            }
            catch (Exception ex)
            {
                var log = _container?.Resolve<ILog>();
                log?.Error($"加载配置失败: {ex.Message}");
            }
            finally
            {
                mainWindowViewModel?.SetAppLoading(false);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {

            try
            {
                var appConfig = _container?.Resolve<AppConfig>();
                var userSettings = _container?.Resolve<IUserSettingsService>();
                // 同步保存用户设置
                if (userSettings != null && appConfig != null)
                {
                    if (!string.IsNullOrEmpty(appConfig.CurrentRecipePath))
                    {
                        userSettings.Settings.LastRecipePath = appConfig.CurrentRecipePath;
                    }
                }
                // 持久化用户设置到磁盘（包含窗口布局、页面布局等）
                userSettings?.SaveSettings();
                //保存配置
                appConfig?.SaveCurrentRecipeAsync();
            }
            catch { }
            base.OnExit(e);
        }
    }
}