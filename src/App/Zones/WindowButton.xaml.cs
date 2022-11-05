namespace LostTech.Stack.Zones {
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Threading;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Interop;
    using System.Windows.Threading;

    using LostTech.Stack.Models;
    using LostTech.Stack.ViewModels;
    using LostTech.Stack.WindowManagement;

    using ManagedShell.Common.Helpers;

    using SystemInformation = System.Windows.Forms.SystemInformation;

    /// <summary>
    /// Interaction logic for WindowButton.xaml
    /// </summary>
    public partial class WindowButton : UserControl, IObjectWithProblems, IDisposable {
        readonly DispatcherTimer hoverTimer = new();
        public WindowButton() {
            this.InitializeComponent();

            this.foregroundTracker = new ForegroundTracker(this,
                window => this.Window?.Equals(window) == true,
                IsForegroundPropertyKey);

            this.hoverTimer.Tick += this.HoverTimer_Tick;
        }

        public AppWindowViewModel ViewModel => this.DataContext as AppWindowViewModel;
        public IAppWindow Window => this.ViewModel?.Window;

        void Window_OnClick(object sender, RoutedEventArgs e) {
#if !PROFILE
            if (App.IsUwp) {
#endif
            this.Window?.Activate();
            return;
#if !PROFILE
            }

            ErrorEventArgs error = ExtraFeatures.PaidFeature("Tabs: Window Buttons");
            this.problems.Add(error.GetException().Message);
            this.ProblemOccurred?.Invoke(this, error);
#endif
        }

        public bool IsForeground => (bool)this.GetValue(IsForegroundPropertyKey.DependencyProperty);
        public static readonly DependencyPropertyKey IsForegroundPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(IsForeground), typeof(bool), typeof(WindowButton), new PropertyMetadata(false));

        readonly ForegroundTracker foregroundTracker;

        readonly List<string> problems = new List<string>();
        public IList<string> Problems => new ReadOnlyCollection<string>(this.problems);
        public event EventHandler<ErrorEventArgs> ProblemOccurred;

        Peeking peeking;
        bool IsPeeking() => this.peeking.Window != IntPtr.Zero;

        protected override void OnMouseEnter(MouseEventArgs e) {
            base.OnMouseEnter(e);

            if (InstantHoverEnabled()) {
                this.StartPeeking();
            } else {
                this.hoverTimer.Interval = TimeSpan.FromMilliseconds(SystemInformation.MouseHoverTime);
                this.hoverTimer.Start();
            }
        }

        void HoverTimer_Tick(object? sender, EventArgs e) {
            this.StartPeeking();
        }

        void StartPeeking() {
            var parentWindow = System.Windows.Window.GetWindow(this);

            if (!this.IsPeeking() && this.Window is Win32Window win32
                && parentWindow is not null
                && ManagedShell.Interop.NativeMethods.DwmIsCompositionEnabled()) {
                this.peeking = new() {
                    Window = win32.Handle,
                    Owner = new WindowInteropHelper(parentWindow).Handle,
                };
                this.peeking.Peek(true);
                EnableInstantHover();
            }
        }

        #region Instant hover
        static readonly ThreadLocal<DispatcherTimer> instantHover = new(() => {
            var timer = new DispatcherTimer();
            timer.Tick += InstantHover_Tick;
            return timer;
        });
        static DispatcherTimer InstantHover => instantHover.Value!;
        static bool InstantHoverEnabled() => true.Equals(InstantHover.Tag);
        static void EnableInstantHover() {
            InstantHover.Tag = true;
            InstantHover.Stop();
        }
        static void DisableInstantHover() {
            InstantHover.Interval = TimeSpan.FromMilliseconds(SystemInformation.MouseHoverTime);
            InstantHover.Start();
        }
        static void InstantHover_Tick(object? sender, EventArgs e) {
            var self = (DispatcherTimer)sender!;
            self.Stop();
            self.Tag = false;
        }
        #endregion

        protected override void OnMouseLeave(MouseEventArgs e) {
            this.StopPeaking();

            base.OnMouseLeave(e);
        }

        void StopPeaking() {
            this.hoverTimer.Stop();
            DisableInstantHover();

            if (this.IsPeeking()
                && ManagedShell.Interop.NativeMethods.DwmIsCompositionEnabled()) {
                this.peeking.Peek(false);
                this.peeking = default;
            }
        }

        void WindowButton_OnUnloaded(object sender, RoutedEventArgs e) {
            this.StopPeaking();

            this.Dispose();
        }

        protected override void OnVisualParentChanged(DependencyObject oldParent) {
            base.OnVisualParentChanged(oldParent);

            this.StopPeaking();

            if (this.VisualParent == null)
                this.Dispose();
        }

        public void Dispose() {
            this.StopPeaking();

            this.foregroundTracker.Dispose();
        }

        void WindowButton_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            this.StopPeaking();
        }

        struct Peeking {
            public IntPtr Owner;
            public IntPtr Window;
            public void Peek(bool show) => WindowHelper.PeekWindow(show, this.Window, this.Owner);
        }
    }
}
