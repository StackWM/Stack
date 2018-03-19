namespace LostTech.Stack.Zones
{
    using System.Windows;
    using System.Windows.Data;
    using LostTech.Stack.ViewModels;
    using ValueConverters;

    public sealed class Layout
    {
        public static bool GetIsHint(DependencyObject obj) => (bool)obj.GetValue(IsHintProperty);
        public static void SetIsHint(DependencyObject obj, bool value) => obj.SetValue(IsHintProperty, value);

        public static readonly DependencyProperty IsHintProperty =
            DependencyProperty.RegisterAttached("IsHint", typeof(bool), typeof(Layout),
                new PropertyMetadata(defaultValue: false,
                    propertyChangedCallback: OnIsHintChanged));

        static void OnIsHintChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (!(d is FrameworkElement element))
                return;

            if (true.Equals(e.NewValue)) {
                var binding = new Binding(nameof(ScreenLayoutViewModel.ShowHints)) {
                    Converter = new BoolToVisibilityConverter(),
                    Mode = BindingMode.OneWay,
                };
                element.SetBinding(UIElement.VisibilityProperty, binding);
            } else {
                BindingOperations.ClearBinding(element, UIElement.VisibilityProperty);
            }
        }
    }
}
