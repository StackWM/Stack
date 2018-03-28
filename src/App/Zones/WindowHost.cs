﻿namespace LostTech.Stack.Zones
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
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
            this.LayoutUpdated += delegate { this.AdjustWindow(); };
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
            Debug.WriteIf(change.OldValue != null, $"Detached from {change.OldValue}");

            this.AdjustWindow();
        }

        public event EventHandler<ErrorEventArgs> NonFatalErrorOccurred;

        Rect lastRect;
        async void AdjustWindow() {
            await Task.Yield();
            Rect? rect = this.TryGetPhysicalBounds();
            if (rect.Equals(this.lastRect) || rect == null)
                return;

            this.lastRect = rect.Value;

            IAppWindow windowToMove = this.Window;
            Exception error = await windowToMove.Move(this.lastRect);
            if (error != null)
                this.NonFatalErrorOccurred?.Invoke(this, new ErrorEventArgs(error));
        }
    }
}