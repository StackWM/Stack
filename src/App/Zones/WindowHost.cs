namespace LostTech.Stack.Zones
{
    using System;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Controls;
    using LostTech.Stack.Models;

    sealed class WindowHost: Control
    {
        public WindowHost() {
            this.HorizontalAlignment = HorizontalAlignment.Stretch;
            this.VerticalAlignment = VerticalAlignment.Stretch;
            this.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            this.VerticalContentAlignment = VerticalAlignment.Stretch;
        }

        public IAppWindow Window {
            get => (IAppWindow)this.GetValue(WindowProperty);
            set => this.SetValue(WindowProperty, value);
        }

        public static readonly DependencyProperty WindowProperty =
            DependencyProperty.Register(nameof(Window), typeof(IAppWindow), typeof(WindowHost),
                new PropertyMetadata(WindowChanged));

        static void WindowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((WindowHost)d).OnWindowChanged(e);
        }

        void OnWindowChanged(DependencyPropertyChangedEventArgs change) {
            if (!(change.NewValue is IAppWindow))
                throw new ArgumentException();

            if (object.ReferenceEquals(change.NewValue, change.OldValue))
                return;

            Debug.WriteLine($"Host attached to {change.NewValue}");
            this.AdjustWindow(new Size(this.ActualWidth, this.ActualHeight));
        }

        Rect lastRect;
        protected override Size ArrangeOverride(Size finalSize) {
            Size size = base.ArrangeOverride(finalSize);
            Rect rect = this.GetPhysicalBounds(size);
            if (rect.Equals(this.lastRect))
                return size;
            this.AdjustWindow(size);
            return size;
        }

        async void AdjustWindow(Size size) {
            Rect rect = this.GetPhysicalBounds(size);
            this.lastRect = rect;
            IAppWindow windowToMove = this.Window;
            Exception problem = await windowToMove.Move(rect);
            if (problem != null)
                Trace.WriteLine($"failed to move {windowToMove}: {problem}");
        }
    }
}
