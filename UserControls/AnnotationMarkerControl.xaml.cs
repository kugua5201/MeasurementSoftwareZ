using MeasurementSoftware.Models;
using System.Windows;
using System.Windows.Controls;

namespace MeasurementSoftware.UserControls
{
    public partial class AnnotationMarkerControl : UserControl
    {
        public static readonly DependencyProperty RecipeProperty =
            DependencyProperty.Register(
                nameof(Recipe),
                typeof(MeasurementRecipe),
                typeof(AnnotationMarkerControl),
                new PropertyMetadata(null));

        public MeasurementRecipe? Recipe
        {
            get => (MeasurementRecipe?)GetValue(RecipeProperty);
            set => SetValue(RecipeProperty, value);
        }

        public static readonly DependencyProperty ForceDefaultColorProperty =
            DependencyProperty.Register(
                nameof(ForceDefaultColor),
                typeof(bool),
                typeof(AnnotationMarkerControl),
                new PropertyMetadata(false));

        /// <summary>
        /// 为 true 时始终显示默认颜色，不受测量结果影响（用于设置页面）
        /// </summary>
        public bool ForceDefaultColor
        {
            get => (bool)GetValue(ForceDefaultColorProperty);
            set => SetValue(ForceDefaultColorProperty, value);
        }

        public AnnotationMarkerControl()
        {
            InitializeComponent();
        }
    }
}
