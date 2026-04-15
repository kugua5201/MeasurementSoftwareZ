using ICSharpCode.AvalonEdit;
using Microsoft.Xaml.Behaviors;
using System.Windows;

namespace MeasurementSoftware.Behaviors
{
    /// <summary>
    /// 为 AvalonEdit 的 TextEditor 提供可绑定的文本属性。
    /// AvalonEdit 的 Text 属性不是 DependencyProperty，不能直接在 XAML 中绑定，
    /// 因此通过 Behavior 做一层双向同步。
    /// </summary>
    public sealed class AvalonEditTextBindingBehavior : Behavior<TextEditor>
    {
        private bool _isInternalUpdate;

        /// <summary>
        /// 可绑定的脚本文本。
        /// </summary>
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(
                nameof(Text),
                typeof(string),
                typeof(AvalonEditTextBindingBehavior),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextPropertyChanged));

        /// <summary>
        /// 获取或设置绑定文本。
        /// </summary>
        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.TextChanged += AssociatedObject_TextChanged;
            SyncEditorText(Text);
        }

        protected override void OnDetaching()
        {
            AssociatedObject.TextChanged -= AssociatedObject_TextChanged;
            base.OnDetaching();
        }

        private static void OnTextPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not AvalonEditTextBindingBehavior behavior || behavior._isInternalUpdate)
            {
                return;
            }

            behavior.SyncEditorText(e.NewValue as string ?? string.Empty);
        }

        private void AssociatedObject_TextChanged(object? sender, EventArgs e)
        {
            if (_isInternalUpdate)
            {
                return;
            }

            _isInternalUpdate = true;
            Text = AssociatedObject.Text ?? string.Empty;
            _isInternalUpdate = false;
        }

        private void SyncEditorText(string text)
        {
            text ??= string.Empty;
            if (AssociatedObject.Text == text)
            {
                return;
            }

            var caretOffset = Math.Clamp(AssociatedObject.CaretOffset, 0, text.Length);
            _isInternalUpdate = true;
            AssociatedObject.Text = text;
            AssociatedObject.CaretOffset = caretOffset;
            _isInternalUpdate = false;
        }
    }
}
