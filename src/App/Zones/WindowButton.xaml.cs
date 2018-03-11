namespace LostTech.Stack.Zones
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using EventHook.Hooks;
    using LostTech.Stack.Models;
    using PInvoke;

    /// <summary>
    /// Interaction logic for WindowButton.xaml
    /// </summary>
    public partial class WindowButton : UserControl
    {
        public WindowButton() {
            this.InitializeComponent();

            this.Unloaded += this.OnUnloaded;
            this.Dispatcher.ShutdownStarted += this.DispatcherOnShutdownStarted;
            this.DataContextChanged += this.OnDataContextChanged;
            this.windowHook.Activated += this.WindowHookOnActivated;
        }

        void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs) {
            if (this.Window != null)
                this.ResetIsForeground(User32.GetForegroundWindow());
        }

        public IAppWindow Window => this.DataContext as IAppWindow;

        void Window_OnClick(object sender, RoutedEventArgs e) => this.Window?.Activate();

        public bool IsForeground => (bool)this.GetValue(IsForegroundPropertyKey.DependencyProperty);
        public static readonly DependencyPropertyKey IsForegroundPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(IsForeground), typeof(bool), typeof(WindowButton), new PropertyMetadata(false));
        void WindowHookOnActivated(object sender, WindowEventArgs windowEventArgs) =>
            this.ResetIsForeground(windowEventArgs.Handle);
        void ResetIsForeground(IntPtr currentForeground) {
            this.SetValue(IsForegroundPropertyKey, this.Window.Equals(new Win32Window(currentForeground)));
        }

        void OnUnloaded(object sender, RoutedEventArgs routedEventArgs) {
            this.windowHook.Dispose();
            this.Dispatcher.ShutdownStarted -= this.DispatcherOnShutdownStarted;
        }
        void DispatcherOnShutdownStarted(object sender, EventArgs eventArgs) {
            this.windowHook.Dispose();
        }
        readonly WindowHookEx windowHook = new WindowHookEx();
    }
}
