using Autofac;
using Autofac.Builder;
using Autofac.Core;
using Autofac.Extras.DynamicProxy;
using MeasurementSoftware.Services.Appearance;
using System.Windows;
using System.Windows.Controls;

namespace MeasurementSoftware.Extensions
{
    /// <summary>
    /// 静态导航服务
    /// </summary>
    public static class Navigation
    {
        /// <summary>
        /// 获取指定类型的页面
        /// </summary>
        public static T? GetPage<T>(string pageName) where T : class => ContainerBuilderExtensions.GetPage<T>(pageName);

        /// <summary>
        /// 获取UserControl页面
        /// </summary>
        public static UserControl? GetPage(string pageName) => ContainerBuilderExtensions.GetPage(pageName);
    }

    /// <summary>
    /// Autofac容器扩展方法
    /// </summary>
    public static class ContainerBuilderExtensions
    {
        #region 私有字段

        private static readonly Dictionary<string, System.Type> PageTypeMapping = new();
        private static IContainer? _container;

        #endregion

        #region 容器设置

        /// <summary>
        /// 设置容器引用
        /// </summary>
        internal static void SetContainer(IContainer container) => _container = container;

        #endregion

        #region 单类型注册

        /// <summary>
        /// 注册瞬时类型
        /// </summary>
        public static ContainerBuilder RegisterTransient<T>(this ContainerBuilder builder) where T : class
                => builder.RegisterWithLifetime<T>(lifetime => lifetime.InstancePerDependency());

        /// <summary>
        /// 注册单例类型
        /// </summary>
        public static ContainerBuilder RegisterSingleton<T>(this ContainerBuilder builder) where T : class
                => builder.RegisterWithLifetime<T>(lifetime => lifetime.SingleInstance());

        /// <summary>
        /// 注册作用域类型
        /// </summary>
        public static ContainerBuilder RegisterScoped<T>(this ContainerBuilder builder) where T : class
                => builder.RegisterWithLifetime<T>(lifetime => lifetime.InstancePerLifetimeScope());

        #endregion

        #region 接口注册

        /// <summary>
        /// 注册瞬时接口
        /// </summary>
        public static ContainerBuilder RegisterTransient<TInterface, TImplementation>(this ContainerBuilder builder) where TImplementation : class, TInterface where TInterface : notnull
                => builder.RegisterInterfaceWithLifetime<TInterface, TImplementation>(lifetime => lifetime.InstancePerDependency());

        /// <summary>
        /// 注册单例接口
        /// </summary>
        public static ContainerBuilder RegisterSingleton<TInterface, TImplementation>(this ContainerBuilder builder) where TImplementation : class, TInterface where TInterface : notnull
                => builder.RegisterInterfaceWithLifetime<TInterface, TImplementation>(lifetime => lifetime.SingleInstance());

        /// <summary>
        /// 注册作用域接口
        /// </summary>
        public static ContainerBuilder RegisterScoped<TInterface, TImplementation>(this ContainerBuilder builder) where TImplementation : class, TInterface where TInterface : notnull
                => builder.RegisterInterfaceWithLifetime<TInterface, TImplementation>(lifetime => lifetime.InstancePerLifetimeScope());





        #endregion

        #region 主窗口注册

        /// <summary>
        /// 注册主窗口和ViewModel (都是单例)
        /// </summary>
        public static ContainerBuilder RegisterMainWindow<TWindow, TViewModel>(this ContainerBuilder builder)
        where TWindow : class
        where TViewModel : class
        {
            // 注册ViewModel为单例
            builder.RegisterSingleton<TViewModel>();
            // 注册Window为单例，并自动注入ViewModel
            builder.RegisterType<TWindow>().AsSelf().SingleInstance().OnActivating(InjectViewModel<TViewModel>);
            return builder;
        }

        #endregion

        #region View和ViewModel注册

        /// <summary>
        /// 注册View和ViewModel (瞬时)
        /// </summary>
        public static ContainerBuilder RegisterViewWithViewModel<TView, TViewModel>(this ContainerBuilder builder, string pageName)
        where TView : class
        where TViewModel : class
        {
            // 注册ViewModel为瞬时
            builder.RegisterTransient<TViewModel>();

            // 注册View为瞬时，并自动注入ViewModel
            builder.RegisterType<TView>().AsSelf().InstancePerDependency().OnActivating(InjectViewModel<TViewModel>);

            // 添加到页面映射
            PageTypeMapping[pageName] = typeof(TView);
            return builder;
        }
        /// <summary>
        /// 带拦截器的View和ViewModel注册 (瞬时)
        /// </summary>
        /// <typeparam name="TView"></typeparam>
        /// <typeparam name="TViewModel"></typeparam>
        /// <typeparam name="TInterceptor"></typeparam>
        /// <param name="builder"></param>
        /// <param name="pageName"></param>
        /// <returns></returns>
        public static ContainerBuilder RegisterViewWithInterceptedViewModel<TView, TViewModel, TInterceptor>(
            this ContainerBuilder builder, string pageName)
            where TView : class
            where TViewModel : class
            where TInterceptor : class
        {
            // 注册ViewModel为瞬时并启用拦截器
            builder.RegisterType<TViewModel>()
                   .AsSelf()
                   .InstancePerDependency()
                   .EnableClassInterceptors()
                   .InterceptedBy(typeof(TInterceptor));

            // 注册View为瞬时，并自动注入ViewModel
            builder.RegisterType<TView>().AsSelf().InstancePerDependency().OnActivating(InjectViewModel<TViewModel>);

            // 添加到页面映射
            PageTypeMapping[pageName] = typeof(TView);
            return builder;
        }


        /// <summary>
        /// 注册View和ViewModel (都是单例)
        /// </summary>
        public static ContainerBuilder RegisterViewWithViewModelSingleton<TView, TViewModel>(this ContainerBuilder builder, string pageName)
        where TView : class
        where TViewModel : class
        {
            // 注册ViewModel为单例
            builder.RegisterSingleton<TViewModel>();

            // 注册View为单例，并自动注入ViewModel
            builder.RegisterType<TView>().AsSelf().SingleInstance().OnActivating(InjectViewModel<TViewModel>);

            // 添加到页面映射
            PageTypeMapping[pageName] = typeof(TView);
            return builder;
        }

        /// <summary>
        /// 注册View和ViewModel (都是作用域)
        /// </summary>
        public static ContainerBuilder RegisterViewWithViewModelScoped<TView, TViewModel>(this ContainerBuilder builder, string pageName)
        where TView : class
        where TViewModel : class
        {
            // 注册ViewModel为作用域
            builder.RegisterScoped<TViewModel>();

            // 注册View为作用域，并自动注入ViewModel
            builder.RegisterType<TView>().AsSelf().InstancePerLifetimeScope().OnActivating(InjectViewModel<TViewModel>);

            // 添加到页面映射
            PageTypeMapping[pageName] = typeof(TView);
            return builder;
        }

        #endregion

        #region 页面获取

        /// <summary>
        /// 获取指定类型的页面
        /// </summary>
        public static T? GetPage<T>(string pageName) where T : class
        {
            if (_container != null && PageTypeMapping.TryGetValue(pageName, out var pageType))
            {
                return _container.Resolve(pageType) as T;
            }
            return null;
        }

        /// <summary>
        /// 获取UserControl页面
        /// </summary>
        public static UserControl? GetPage(string pageName) => GetPage<UserControl>(pageName);

        /// <summary>
        /// 获取服务
        /// </summary>
        public static T? GetService<T>() where T : class
        {
            return _container?.Resolve<T>();
        }

        #endregion

        #region 私有辅助方法

        /// <summary>
        /// 通用类型注册辅助方法
        /// </summary>
        private static ContainerBuilder RegisterWithLifetime<T>(this ContainerBuilder builder, Action<IRegistrationBuilder<T, IConcreteActivatorData, SingleRegistrationStyle>> lifetimeConfig) where T : class
        {
            var registration = builder.RegisterType<T>().AsSelf();
            lifetimeConfig(registration);
            return builder;
        }


        /// <summary>
        /// 通用接口注册辅助方法
        /// </summary>
        private static ContainerBuilder RegisterInterfaceWithLifetime<TInterface, TImplementation>(this ContainerBuilder builder, Action<IRegistrationBuilder<TImplementation, IConcreteActivatorData, SingleRegistrationStyle>> lifetimeConfig)
                 where TImplementation : class, TInterface
                 where TInterface : notnull
        {
            var registration = builder.RegisterType<TImplementation>().As<TInterface>();
            lifetimeConfig(registration);
            return builder;
        }




        /// <summary>
        /// ViewModel自动注入辅助方法
        /// </summary>
        private static void InjectViewModel<TViewModel>(IActivatingEventArgs<object> e) where TViewModel : class
        {
            if (_container != null && e.Instance is FrameworkElement view)
            {
                var viewModel = _container.Resolve<TViewModel>();
                view.DataContext = viewModel;
                _container.ResolveOptional<IAppAppearanceService>()?.Attach(view);
            }
        }

        #endregion
    }
}