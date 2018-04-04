namespace LostTech.Stack.Zones
{
    using System.Diagnostics;
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

        public static int GetVersion(DependencyObject obj) {
            Debug.Assert(!(obj is ScreenLayout));
            return (int)obj.GetValue(VersionProperty);
        }

        public static void SetVersion(DependencyObject obj, int value) => obj.SetValue(VersionProperty, value);
        public static readonly DependencyProperty VersionProperty =
            DependencyProperty.RegisterAttached("Version", typeof(int), typeof(Layout),
                new PropertyMetadata(defaultValue: 1));

        internal static string GetSource(DependencyObject obj) => (string)obj.GetValue(SourceProperty);
        internal static void SetSource(DependencyObject obj, string value) => obj.SetValue(SourceProperty, value);
        static readonly DependencyProperty SourceProperty =
            DependencyProperty.RegisterAttached("Source", typeof(string), typeof(Layout));

        public class Version
        {
            public const int Current = 2;

            public class Min
            {
                public const int PermanentlyVisible = 2;
            }
        }
    }
}
