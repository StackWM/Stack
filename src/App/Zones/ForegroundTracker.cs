namespace LostTech.Stack.Zones
{
    using System;
    using System.Windows;
    using EventHook.Hooks;
    using LostTech.Stack.Models;
    using PInvoke;

    class ForegroundTracker
    {
        readonly FrameworkElement attachedTo;
        readonly Func<IAppWindow, bool> isForegroundLambda;
        readonly DependencyPropertyKey isForegroundKey;
        readonly Win32WindowFactory win32WindowFactory = new Win32WindowFactory();

        public WindowHookEx Hook { get; } = new WindowHookEx();

        public ForegroundTracker(FrameworkElement attachTo,
            Func<IAppWindow, bool> isForegroundLambda,
            DependencyPropertyKey isForegroundKey) {
            this.attachedTo = attachTo ?? throw new ArgumentNullException(nameof(attachTo));
            this.isForegroundLambda = isForegroundLambda;
            this.isForegroundKey = isForegroundKey;
            attachTo.Unloaded += this.OnUnloaded;
            attachTo.Dispatcher.ShutdownStarted += this.DispatcherOnShutdownStarted;
            attachTo.DataContextChanged += this.OnDataContextChanged;
            this.Hook.Activated += this.WindowHookOnActivated;
        }

        void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs) {
           this.WindowHookOnActivated(this, new WindowEventArgs(User32.GetForegroundWindow()));
        }

        void WindowHookOnActivated(object sender, WindowEventArgs e) {
            bool isForeground = this.isForegroundLambda(this.win32WindowFactory.Create(e.Handle));
            this.attachedTo.SetValue(this.isForegroundKey, isForeground);
        }

        void OnUnloaded(object sender, RoutedEventArgs routedEventArgs) {
            this.attachedTo.DataContextChanged -= this.OnDataContextChanged;
            this.Hook.Dispose();
            this.attachedTo.Dispatcher.ShutdownStarted -= this.DispatcherOnShutdownStarted;
        }
        void DispatcherOnShutdownStarted(object sender, EventArgs eventArgs) {
            this.attachedTo.DataContextChanged -= this.OnDataContextChanged;
            this.Hook.Dispose();
        }
    }
}
