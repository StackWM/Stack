namespace LostTech.Stack.Zones {
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using LostTech.Stack.Utils;
    using LostTech.Stack.ViewModels;
    using LostTech.Stack.WindowManagement;
    using Microsoft.Win32;
    using Rect = System.Drawing.RectangleF;

    sealed class WindowHost: Control
    {
        public WindowHost() {
            this.HorizontalAlignment = HorizontalAlignment.Stretch;
            this.VerticalAlignment = VerticalAlignment.Stretch;
            this.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            this.VerticalContentAlignment = VerticalAlignment.Stretch;
            this.LayoutUpdated += delegate { this.AdjustWindow(); };
            SystemEvents.SessionSwitch += this.OnSessionSwitch;
        }

        public AppWindowViewModel Window {
            get => (AppWindowViewModel)this.GetValue(WindowProperty);
            set => this.SetValue(WindowProperty, value);
        }

        public static readonly DependencyProperty WindowProperty =
            DependencyProperty.Register(nameof(Window), typeof(AppWindowViewModel), typeof(WindowHost),
                new PropertyMetadata(WindowChanged));

        static void WindowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((WindowHost)d).OnWindowChanged(e);
        }

        void OnWindowChanged(DependencyPropertyChangedEventArgs change) {
            if (!(change.NewValue is AppWindowViewModel))
                throw new ArgumentException();

            if (object.ReferenceEquals(change.NewValue, change.OldValue))
                return;

            Debug.WriteLine($"Host attached to {change.NewValue}");
            Debug.WriteIf(change.OldValue != null, $"Detached from {change.OldValue}");

            this.AdjustWindow();
        }

        public event EventHandler<ErrorEventArgs> NonFatalErrorOccurred;

        Rect lastRect, newRect;
        readonly Throttle adjustThrottle = new Throttle {MinimumDelay = TimeSpan.FromSeconds(1f / 30)};
        async void AdjustWindow() {
            await Task.Yield();
            Rect? rect = this.TryGetPhysicalBounds();
            Thread.MemoryBarrier();
            if (rect.Equals(this.lastRect) || rect == null)
                return;

            this.newRect = rect.Value;
            Thread.MemoryBarrier();

            if (!await this.adjustThrottle.TryAcquire())
                return;

            Thread.MemoryBarrier();
            if (this.newRect != rect.Value)
                return;

            this.lastRect = rect.Value;
            Thread.MemoryBarrier();

            IAppWindow windowToMove = this.Window.Window;
            try {
                await windowToMove.Move(rect.Value).ConfigureAwait(false);
            } catch (WindowNotFoundException) {
            } catch (Exception error) {
                this.NonFatalErrorOccurred?.Invoke(this, new ErrorEventArgs(error));
            }
        }

        void OnSessionSwitch(object sender, SessionSwitchEventArgs e) {
            switch (e.Reason) {
            case SessionSwitchReason.ConsoleConnect:
            case SessionSwitchReason.RemoteConnect:
            case SessionSwitchReason.SessionUnlock:
                this.lastRect = Rect.Empty;
                Thread.MemoryBarrier();
                break;
            }
        }
    }
}
