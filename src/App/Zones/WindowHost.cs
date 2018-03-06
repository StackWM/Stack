﻿namespace LostTech.Stack.Zones
{
    using System;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Controls;
    using LostTech.Stack.Models;

    sealed class WindowHost: Control
    {
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
            this.AdjustWindow();
        }


        Rect lastRect;
        protected override Size ArrangeOverride(Size finalSize) {
            Size size = base.ArrangeOverride(finalSize);
            Rect rect = this.GetPhysicalBounds();
            if (rect.Equals(this.lastRect))
                return size;
            this.AdjustWindow();
            return size;
        }

        async void AdjustWindow() {
            Rect rect = this.GetPhysicalBounds();
            this.lastRect = rect;
            IAppWindow windowToMove = this.Window;
            Exception problem = await windowToMove.Move(rect);
            if (problem != null)
                Trace.WriteLine($"failed to move {windowToMove}: {problem}");
        }
    }
}
