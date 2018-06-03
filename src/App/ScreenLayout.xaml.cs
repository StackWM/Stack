namespace LostTech.Stack
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Interop;
    using System.Windows.Media;
    using LostTech.Stack.ScreenCoordinates;
    using LostTech.Stack.ViewModels;
    using LostTech.Stack.Utils;
    using LostTech.Stack.Zones;
    using LostTech.Windows;
    using MahApps.Metro.Controls;
    using Microsoft.Win32;
    using PInvoke;

    /// <summary>
    /// Interaction logic for ScreenLayout.xaml
    /// </summary>
    public partial class ScreenLayout
    {
        internal HwndSource handle;

        public ScreenLayout()
        {
            this.InitializeComponent();
            this.Show();
            // this also makes window to be visible on all virtual desktops
            this.SetIsListedInTaskSwitcher(false);
            SystemEvents.SessionSwitch += this.OnSessionSwitch;
        }

        public void SetLayout(FrameworkElement layout) {
            layout.Width = layout.Height = double.NaN;
            this.Content = layout;
            var readiness = new TaskCompletionSource<bool>();
            Stack.Zones.Layout.SetReady(layout, readiness.Task);

            layout.Loaded += delegate {
                if (this.windowPositioned) {
                    this.ready.TrySetResult(true);
                    readiness.TrySetResult(true);
                }
            };
            layout.Unloaded += delegate { readiness.TrySetCanceled(); };
        }

        public FrameworkElement Layout => this.Content as FrameworkElement;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            this.handle = (HwndSource)PresentationSource.FromVisual(this);
            // ReSharper disable once PossibleNullReferenceException
            this.handle.AddHook(this.OnWindowMessage);
            this.SetScreen(this.Screen);
        }

        readonly TaskCompletionSource<bool> loaded = new TaskCompletionSource<bool>();
        void OnLoaded(object sender, EventArgs e) => this.loaded.TrySetResult(true);

        protected override void OnClosed(EventArgs e)
        {
            this.handle?.RemoveHook(this.OnWindowMessage);
            this.handle = null;
            base.OnClosed(e);
        }

        protected internal bool IsHandleInitialized => this.handle != null;

        private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch ((User32.WindowMessage) msg) {
            case User32.WindowMessage.WM_SETTINGCHANGE:
                this.AdjustToScreenWhenIdle();
                break;
            }
            return IntPtr.Zero;
        }

        void OnSessionSwitch(object sender, SessionSwitchEventArgs e) {
            switch (e.Reason) {
            case SessionSwitchReason.ConsoleConnect:
            case SessionSwitchReason.RemoteConnect:
            case SessionSwitchReason.SessionUnlock:
                this.AdjustToScreenWhenIdle();
                break;
            }
        }

        Win32Screen lastScreen;
        public Win32Screen Screen => this.ViewModel?.Screen;

        internal ScreenLayoutViewModel ViewModel {
            get => (ScreenLayoutViewModel)this.DataContext;
            set => this.DataContext = value;
        }

        void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (e.OldValue is ScreenLayoutViewModel viewModel)
                viewModel.PropertyChanged -= this.OnViewModelPropertyChanged;
            if (e.NewValue is ScreenLayoutViewModel newViewModel) {
                newViewModel.PropertyChanged += this.OnViewModelPropertyChanged;
                this.SetScreen(newViewModel.Screen);
            }
        }

        void SetScreen(Win32Screen newScreen) {
            if (this.lastScreen == newScreen)
                return;

            if (this.lastScreen != null)
                this.lastScreen.PropertyChanged -= this.OnScreenPropertyChanged;
            this.lastScreen = newScreen;
            if (this.lastScreen != null) {
                this.lastScreen.PropertyChanged += this.OnScreenPropertyChanged;
                this.AdjustToScreenWhenIdle();
            }
        }

        void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
            case nameof(ScreenLayoutViewModel.Screen):
                this.SetScreen(this.ViewModel?.Screen);
                break;
            }
        }

        void OnScreenPropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
            case nameof(this.Screen.WorkingArea):
            case nameof(this.Screen.TransformFromDevice):
                this.AdjustToScreenWhenIdle();
                break;
            }
        }

        public void AdjustToClientArea()
        {
            if (this.Screen != null)
                this.AdjustToClientArea(this.Screen);
            else
                throw new InvalidOperationException();
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);

            this.AdjustToScreenWhenIdle();
        }

        Task idleAdjustDelay;
        async void AdjustToScreenWhenIdle() {
            var delay = Task.Delay(millisecondsDelay: 500);
            this.idleAdjustDelay = delay;
            await Task.WhenAll(delay, this.loaded.Task);
            if (delay != this.idleAdjustDelay)
                return;
            if (this.IsLoaded)
                this.AdjustToScreen();
        }

        readonly TaskCompletionSource<bool> ready = new TaskCompletionSource<bool>();
        bool windowPositioned = false;
        /// <summary>
        /// Completes, when this instance is adjusted to the screen, and some layout is loaded
        /// </summary>
        public Task Ready => this.ready.Task;
        async void AdjustToScreen()
        {
            for (int retry = 0; retry < 8; retry++) {
                if (this.Screen == null || !this.IsHandleInitialized)
                    return;

                var opacity = this.Opacity;
                var visibility = this.Visibility;
                this.Opacity = 0;
                try {
                    this.Show();
                } catch (InvalidOperationException) {
                    await Task.Delay(400);
                    continue;
                }
                Debug.WriteLine($"adjusting {this.Title} to {this.Screen.WorkingArea}");
                this.AdjustToClientArea(this.Screen);
                this.Visibility = visibility;
                this.Opacity = opacity;
                this.windowPositioned = true;
                if (this.Layout != null) {
                    await Task.Yield();
                    this.ready.TrySetResult(true);
                }
                return;
            }
        }

        public bool TryShow() {
            if (!this.IsHandleInitialized)
                return false;
            try {
                this.Show();
                return true;
            } catch (InvalidOperationException) {
                return false;
            }
        }

        public bool TryHide() {
            if (!this.IsHandleInitialized)
                return false;
            try {
                this.Hide();
                return true;
            } catch (InvalidOperationException) {
                return false;
            }
        }

        public IEnumerable<Zone> Zones => this.FindChildren<Zone>();

        internal Zone GetZone(Point dropPoint)
        {
            Zone result = null;
            VisualTreeHelper.HitTest(this,
                target => {
                    result = target as Zone;
                    return result == null ? HitTestFilterBehavior.Continue : HitTestFilterBehavior.Stop;
                },
                _ => HitTestResultBehavior.Stop,
                new PointHitTestParameters(dropPoint));
            return result;
        }
    }

    static class ScreenLayoutExtensions
    {
        public static IEnumerable<ScreenLayout> Active(this IEnumerable<ScreenLayout> layouts)
            => layouts.Where(layout => layout.Screen.IsActive);
    }
}
