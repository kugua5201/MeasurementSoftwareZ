using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace MeasurementSoftware.Extensions
{
    //图标官网
    public enum FontAwesomeStyle
    {
        /// <summary>
        /// 常规
        /// </summary>
        Regular,

        /// <summary>
        /// 实心的
        /// </summary>
        Solid,

        /// <summary>
        /// 品牌（Logo）
        /// </summary>
        Brands
    }
    public class FontAwesomeIcon : TextBlock
    {

        #region Icon 属性
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(nameof(Icon), typeof(string), typeof(FontAwesomeIcon),
                new PropertyMetadata(null, (d, e) => ((FontAwesomeIcon)d).Text = e.NewValue?.ToString()));

        public string Icon
        {
            get => (string)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }
        #endregion

        #region Spin 属性
        public static readonly DependencyProperty SpinProperty =
            DependencyProperty.Register(nameof(Spin), typeof(bool), typeof(FontAwesomeIcon),
                new PropertyMetadata(false, (d, e) => ((FontAwesomeIcon)d).UpdateAnimation()));

        public bool Spin
        {
            get => (bool)GetValue(SpinProperty);
            set => SetValue(SpinProperty, value);
        }
        #endregion

        #region SpinSpeed 属性
        public static readonly DependencyProperty SpinSpeedProperty =
            DependencyProperty.Register(nameof(SpinSpeed), typeof(double), typeof(FontAwesomeIcon),
                new PropertyMetadata(1.0, (d, e) =>
                {
                    var fa = (FontAwesomeIcon)d;
                    if (fa.Spin)
                        fa.StartSpin();
                }));

        public double SpinSpeed
        {
            get => (double)GetValue(SpinSpeedProperty);
            set => SetValue(SpinSpeedProperty, value);
        }
        #endregion

        #region Beat 属性
        public static readonly DependencyProperty BeatProperty =
            DependencyProperty.Register(nameof(Beat), typeof(bool), typeof(FontAwesomeIcon),
                new PropertyMetadata(false, (d, e) => ((FontAwesomeIcon)d).UpdateAnimation()));

        public bool Beat
        {
            get => (bool)GetValue(BeatProperty);
            set => SetValue(BeatProperty, value);
        }
        #endregion

        #region BeatScale 属性
        public static readonly DependencyProperty BeatScaleProperty =
            DependencyProperty.Register(nameof(BeatScale), typeof(double), typeof(FontAwesomeIcon),
                new PropertyMetadata(1.3));

        public double BeatScale
        {
            get => (double)GetValue(BeatScaleProperty);
            set => SetValue(BeatScaleProperty, value);
        }
        #endregion

        #region BeatDuration 属性
        public static readonly DependencyProperty BeatDurationProperty =
            DependencyProperty.Register(nameof(BeatDuration), typeof(double), typeof(FontAwesomeIcon),
                new PropertyMetadata(0.5));

        public double BeatDuration
        {
            get => (double)GetValue(BeatDurationProperty);
            set => SetValue(BeatDurationProperty, value);
        }
        #endregion

        #region IconFamily 属性
        public static readonly DependencyProperty IconFamilyProperty =
            DependencyProperty.Register(nameof(IconFamily), typeof(FontAwesomeStyle), typeof(FontAwesomeIcon),
                new PropertyMetadata(FontAwesomeStyle.Regular, OnIconFamilyChanged));

        public FontAwesomeStyle IconFamily
        {
            get => (FontAwesomeStyle)GetValue(IconFamilyProperty);
            set => SetValue(IconFamilyProperty, value);
        }

        private static void OnIconFamilyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var fa = (FontAwesomeIcon)d;
            fa.UpdateFontFamily();
        }

        private void UpdateFontFamily()
        {
            FontFamily = IconFamily switch
            {
                FontAwesomeStyle.Regular => (FontFamily)Application.Current.Resources["FontAwesomeRegular"],
                FontAwesomeStyle.Solid => (FontFamily)Application.Current.Resources["FontAwesomeSolid"],
                FontAwesomeStyle.Brands => (FontFamily)Application.Current.Resources["FontAwesomeBrands"],
                _ => (FontFamily)Application.Current.Resources["FontAwesomeRegular"]
            };
        }
        #endregion

        // Transform components
        private RotateTransform? _rotateTransform;
        private ScaleTransform? _scaleTransform;
        private TransformGroup? _transformGroup;

        private bool _initialized = false;
        private DoubleAnimation? _currentSpinAnimation;

        public FontAwesomeIcon()
        {
            // 设置文本对齐方式，确保内容居中
            TextAlignment = TextAlignment.Center;
            VerticalAlignment = VerticalAlignment.Center;
            HorizontalAlignment = HorizontalAlignment.Center;

            // 设置默认字体家庭
            UpdateFontFamily();

            // 创建 transforms
            _rotateTransform = new RotateTransform();
            _scaleTransform = new ScaleTransform(1, 1);
            _transformGroup = new TransformGroup();
            _transformGroup.Children.Add(_scaleTransform);
            _transformGroup.Children.Add(_rotateTransform);

            // 使用 RenderTransform 避免布局抖动
            RenderTransform = _transformGroup;
            // 关键：设置变换原点为内容中心
            RenderTransformOrigin = new Point(0.5, 0.5);

            // 延迟初始化到 Loaded
            Loaded += OnLoadedSafe;
            Unloaded += OnUnloadedSafe;
            SizeChanged += OnSizeChangedSafe;
        }

        private void OnLoadedSafe(object? sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                if (_initialized) return;
                _initialized = true;

                UpdateAnimation();
            }), DispatcherPriority.Loaded);
        }

        private void OnSizeChangedSafe(object? sender, SizeChangedEventArgs e)
        {
            // 确保变换原点始终居中
            RenderTransformOrigin = new Point(0.5, 0.5);
        }

        private void OnUnloadedSafe(object? sender, RoutedEventArgs e)
        {
            // 停止动画，避免内存泄漏
            StopSpin();
            StopBeat();
        }

        private void UpdateAnimation()
        {
            if (DesignerProperties.GetIsInDesignMode(this)) return;

            if (!_initialized)
            {
                return;
            }

            if (Spin) StartSpin(); else StopSpin();
            if (Beat) StartBeat(); else StopBeat();
        }

        private void StartSpin()
        {
            if (_rotateTransform == null) return;

            // 重新确认 RenderTransform 已正确赋值
            if (RenderTransform != _transformGroup)
            {
                RenderTransform = _transformGroup;
                RenderTransformOrigin = new Point(0.5, 0.5);
            }

            // 先取消旧动画
            StopSpin();

            // 方法1：使用 By 动画实现无缝旋转（推荐）
            var anim = new DoubleAnimation
            {
                By = 360, // 每次增加360度
                Duration = TimeSpan.FromSeconds(Math.Max(0.01, SpinSpeed)),
                RepeatBehavior = RepeatBehavior.Forever
            };

            // 动画在 WPF 内部直接使用 Freezable 缓存，提高性能
            anim.Freeze();

            _currentSpinAnimation = anim;
            _rotateTransform.BeginAnimation(RotateTransform.AngleProperty, anim);

            // 方法2：使用 IsCumulative 属性（备选方案）
            /*
            var anim = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(Math.Max(0.01, SpinSpeed))))
            {
                RepeatBehavior = RepeatBehavior.Forever,
                IsCumulative = true // 累积值，避免跳回0度
            };
            */
        }

        private void StopSpin()
        {
            _rotateTransform?.BeginAnimation(RotateTransform.AngleProperty, null);
            _currentSpinAnimation = null;

            // 重置旋转角度
            if (_rotateTransform != null)
                _rotateTransform.Angle = 0;
        }

        private void StartBeat()
        {
            if (_scaleTransform == null)
            {
                return;
            }

            // 重新确认 RenderTransform 已正确赋值
            if (RenderTransform != _transformGroup)
            {
                RenderTransform = _transformGroup;
                RenderTransformOrigin = new Point(0.5, 0.5);
            }

            // 先停止已有缩放动画
            _scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            _scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);

            // 使用更平滑的缓动函数
            var anim = new DoubleAnimation(1.0, BeatScale, new Duration(TimeSpan.FromSeconds(Math.Max(0.01, BeatDuration))))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new QuadraticEase() { EasingMode = EasingMode.EaseInOut }
            };

            _scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            _scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        }

        private void StopBeat()
        {
            if (_scaleTransform != null)
            {
                _scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                _scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                _scaleTransform.ScaleX = 1.0;
                _scaleTransform.ScaleY = 1.0;
            }
        }
    }
}
