namespace LostTech.Stack.Zones
{
    using System;
    using System.Windows;
    using EventHook.Hooks;
    using PInvoke;

    class ForegroundTracker
    {
        readonly WindowHookEx windowHook = new WindowHookEx();
        readonly FrameworkElement attachedTo;
        readonly Func<IntPtr, bool> isForegroundLambda;
        readonly DependencyPropertyKey isForegroundKey;

        public ForegroundTracker(FrameworkElement attachTo,
            Func<IntPtr, bool> isForegroundLambda,
            DependencyPropertyKey isForegroundKey) {
            this.attachedTo = attachTo ?? throw new ArgumentNullException(nameof(attachTo));
            this.isForegroundLambda = isForegroundLambda;
            this.isForegroundKey = isForegroundKey;
            attachTo.Unloaded += this.OnUnloaded;
            attachTo.Dispatcher.ShutdownStarted += this.DispatcherOnShutdownStarted;
            attachTo.DataContextChanged += this.OnDataContextChanged;
            this.windowHook.Activated += this.WindowHookOnActivated;
        }

        void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs) {
           this.WindowHookOnActivated(this, new WindowEventArgs(User32.GetForegroundWindow()));
        }

        void WindowHookOnActivated(object sender, WindowEventArgs e) {
            bool isForeground = this.isForegroundLambda(e.Handle);
            this.attachedTo.SetValue(this.isForegroundKey, isForeground);
        }

        void OnUnloaded(object sender, RoutedEventArgs routedEventArgs) {
            this.attachedTo.DataContextChanged -= this.OnDataContextChanged;
            this.windowHook.Dispose();
            this.attachedTo.Dispatcher.ShutdownStarted -= this.DispatcherOnShutdownStarted;
        }
        void DispatcherOnShutdownStarted(object sender, EventArgs eventArgs) {
            this.attachedTo.DataContextChanged -= this.OnDataContextChanged;
            this.windowHook.Dispose();
        }
    }
}
