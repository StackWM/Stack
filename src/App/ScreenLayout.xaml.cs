namespace LostTech.Stack
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.InteropServices;
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
    using PInvoke;

    /// <summary>
    /// Interaction logic for ScreenLayout.xaml
    /// </summary>
    public partial class ScreenLayout
    {
        HwndSource handle;

        public ScreenLayout()
        {
            this.InitializeComponent();
            this.Show();
            //this.TryEnableGlassEffect();
        }

        public void SetLayout(FrameworkElement layout) {
            layout.Width = layout.Height = double.NaN;
            this.Content = layout;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            this.handle = (HwndSource)PresentationSource.FromVisual(this);
            // ReSharper disable once PossibleNullReferenceException
            this.handle.AddHook(this.OnWindowMessage);
        }

        protected override void OnClosed(EventArgs e)
        {
            this.handle?.RemoveHook(this.OnWindowMessage);
            this.handle = null;
            base.OnClosed(e);
        }

        protected bool IsHandleInitialized => this.handle != null;

        private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch ((User32.WindowMessage) msg) {
            case User32.WindowMessage.WM_SETTINGCHANGE:
                this.AdjustToScreenWhenIdle();
                break;
            }
            return IntPtr.Zero;
        }

        Win32Screen lastScreen;
        public Win32Screen Screen => this.ViewModel.Screen;

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
            if (this.DataContext is Win32Screen screen)
                this.AdjustToClientArea(screen);
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
            var delay = Task.Delay(millisecondsDelay: 1000);
            this.idleAdjustDelay = delay;
            await delay;
            if (delay == this.idleAdjustDelay && this.IsLoaded)
                this.AdjustToScreen();
        }

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
                this.AdjustToClientArea(this.Screen);
                this.Visibility = visibility;
                this.Opacity = opacity;
                return;
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
