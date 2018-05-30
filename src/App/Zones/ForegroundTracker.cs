namespace LostTech.Stack.Zones
{
    using System;
    using System.Windows;
    using System.Windows.Threading;
    using EventHook.Hooks;
    using LostTech.Stack.Models;
    using LostTech.Stack.WindowManagement;
    using PInvoke;

    class ForegroundTracker: IDisposable
    {
        readonly FrameworkElement attachedTo;
        readonly Func<IAppWindow, bool> isForegroundLambda;
        readonly DependencyPropertyKey isForegroundKey;
        readonly Win32WindowFactory win32WindowFactory = new Win32WindowFactory();

        public WindowHookEx Hook { get; } = WindowHookExFactory.Instance.GetHook();

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
            this.Dispose();
        }
        void DispatcherOnShutdownStarted(object sender, EventArgs eventArgs) {
            this.Dispose();
        }

        public void Dispose() {
            this.Hook.Activated -= this.WindowHookOnActivated;
            this.attachedTo.DataContextChanged -= this.OnDataContextChanged;
            Dispatcher dispatcher = this.attachedTo.Dispatcher;
            if (dispatcher != null)
                dispatcher.ShutdownStarted -= this.DispatcherOnShutdownStarted;
        }
    }
}
