namespace LostTech.Stack.Zones
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Data;
    using JetBrains.Annotations;
    using LostTech.Stack.ViewModels;
    using ValueConverters;

    public sealed class Layout
    {
        #region IsHint
        static readonly BoolToVisibilityConverter boolToVisibility = new BoolToVisibilityConverter();

        public static bool GetIsHint([NotNull] DependencyObject obj) {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            return (bool)obj.GetValue(IsHintProperty);
        }
        public static void SetIsHint([NotNull] DependencyObject obj, bool value) {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            obj.SetValue(IsHintProperty, value);
        }

        public static readonly DependencyProperty IsHintProperty =
            DependencyProperty.RegisterAttached("IsHint", typeof(bool), typeof(Layout),
                new PropertyMetadata(defaultValue: false,
                    propertyChangedCallback: OnIsHintChanged));

        static void OnIsHintChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (!(d is FrameworkElement element))
                return;

            if (true.Equals(e.NewValue)) {
                var binding = new Binding(nameof(ScreenLayoutViewModel.ShowHints)) {
                    Converter = boolToVisibility,
                    Mode = BindingMode.OneWay,
                };
                element.SetBinding(UIElement.VisibilityProperty, binding);
            } else {
                BindingOperations.ClearBinding(element, UIElement.VisibilityProperty);
            }
        }
        #endregion

        #region IsHint
        static readonly BoolToVisibilityConverter boolToVisibilityInv = new BoolToVisibilityConverter{IsInverted = true};

        public static bool GetIsUnderlay([NotNull] DependencyObject obj) {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            return (bool)obj.GetValue(IsUnderlayProperty);
        }
        public static void SetIsUnderlay([NotNull] DependencyObject obj, bool value) {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            obj.SetValue(IsUnderlayProperty, value);
        }

        public static readonly DependencyProperty IsUnderlayProperty =
            DependencyProperty.RegisterAttached("IsUnderlay", typeof(bool), typeof(Layout),
                new PropertyMetadata(defaultValue: false,
                    propertyChangedCallback: OnIsUnderlayChanged));

        static void OnIsUnderlayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (!(d is FrameworkElement element))
                return;

            if (true.Equals(e.NewValue)) {
                var binding = new Binding(nameof(ScreenLayoutViewModel.ShowHints)) {
                    Converter = boolToVisibilityInv,
                    Mode = BindingMode.OneWay,
                };
                element.SetBinding(UIElement.VisibilityProperty, binding);
            } else {
                BindingOperations.ClearBinding(element, UIElement.VisibilityProperty);
            }
        }
        #endregion

        #region Ready
        internal static readonly DependencyPropertyKey ReadyPropertyKey =
            DependencyProperty.RegisterAttachedReadOnly("Ready", typeof(Task), typeof(FrameworkElement), new PropertyMetadata(null));
        public static readonly DependencyProperty ReadyProperty = ReadyPropertyKey.DependencyProperty;
        public static Task GetReady([NotNull] DependencyObject obj) {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            return (Task)obj.GetValue(ReadyProperty);
        }
        internal static void SetReady([NotNull] DependencyObject obj, Task value) {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            obj.SetValue(ReadyPropertyKey, value);
        }

        #endregion

        #region Version
        public static int GetVersion([NotNull] DependencyObject obj) {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            Debug.Assert(!(obj is ScreenLayout));
            return (int)obj.GetValue(VersionProperty);
        }
        public static void SetVersion([NotNull] DependencyObject obj, int value) {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            obj.SetValue(VersionProperty, value);
        }
        public static readonly DependencyProperty VersionProperty =
            DependencyProperty.RegisterAttached("Version", typeof(int), typeof(Layout),
                new PropertyMetadata(defaultValue: 1));

        public class Version {
            public const int Current = 2;

            public class Min {
                public const int PermanentlyVisible = 2;
            }
        }
        #endregion

        #region Source
        internal static string GetSource([NotNull] DependencyObject obj) {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            return (string)obj.GetValue(SourceProperty);
        }
        internal static void SetSource([NotNull] DependencyObject obj, string value) {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            obj.SetValue(SourceProperty, value);
        }
        static readonly DependencyProperty SourceProperty =
            DependencyProperty.RegisterAttached("Source", typeof(string), typeof(Layout));
        #endregion
    }
}
