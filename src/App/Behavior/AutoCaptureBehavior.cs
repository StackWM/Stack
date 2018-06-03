namespace LostTech.Stack.Behavior
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using EventHook.Hooks;
    using JetBrains.Annotations;
    using LostTech.Stack.Models;
    using LostTech.Stack.Settings;
    using LostTech.Stack.Utils;
    using LostTech.Stack.ViewModels;
    using LostTech.Stack.WindowManagement;
    using LostTech.Stack.Zones;
    using Rect = System.Drawing.RectangleF;

    class AutoCaptureBehavior: IDisposable
    {
        readonly GeneralBehaviorSettings settings;
        readonly LayoutManager layoutManager;
        readonly ILayoutsViewModel layouts;
        readonly Win32WindowFactory win32WindowFactory;
        readonly WindowHookEx activationHook = WindowHookExFactory.Instance.GetHook();
        readonly TaskScheduler taskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

        public AutoCaptureBehavior(
            [NotNull] GeneralBehaviorSettings settings,
            [NotNull] LayoutManager layoutManager,
            [NotNull] ILayoutsViewModel layouts,
            [NotNull] Win32WindowFactory win32WindowFactory)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.layoutManager = layoutManager ?? throw new ArgumentNullException(nameof(layoutManager));
            this.layouts = layouts ?? throw new ArgumentNullException(nameof(layouts));
            this.win32WindowFactory = win32WindowFactory ?? throw new ArgumentNullException(nameof(win32WindowFactory));
            this.BeginInitAsync();
        }

        async void BeginInitAsync() {
            await Task.WhenAll(this.layouts.ScreenLayouts.Active().Select(layout => layout.Ready));

            await Task.Yield();

            if (this.settings.CaptureOnStackStart)
                this.Capture();

            this.layoutManager.WindowAppeared += this.OnWindowAppeared;
            this.layoutManager.DesktopSwitched += this.OnDesktopSwitched;
            this.layouts.LayoutLoaded += this.OnLayoutLoaded;
            this.activationHook.Activated += this.OnWindowActivated;
        }

        async void OnLayoutLoaded(object sender, EventArgs<ScreenLayout> args) {
            if (!this.settings.CaptureOnLayoutChange)
                return;

            if (args.Subject?.Layout == null)
                return;

            await Task.Yield();

            Rect bounds = args.Subject.Layout.GetPhysicalBounds();
            await Task.Factory.StartNew(() =>
                this.win32WindowFactory
                    .ForEachTopLevel(async window => {
                        try {
                            Rect intersection = window.Bounds.Intersection(bounds);
                            if (intersection.IsEmpty || intersection.Width < 10 || intersection.Height < 10)
                                return;

                            if (this.win32WindowFactory.DisplayInSwitchToList(window))
                                await this.Capture(window);
                        } catch (Exception e) {
                            e.ReportAsWarning();
                        }
                    })
                    .ReportAsWarning()).ConfigureAwait(false);
        }

        void OnDesktopSwitched(object sender, EventArgs e) {
            if (this.settings.CaptureOnDesktopSwitch)
                Task.Factory.StartNew(this.Capture).ReportAsWarning();
        }

        void OnWindowAppeared(object sender, EventArgs<IAppWindow> args) {
            if (this.settings.CaptureOnAppStart)
                Task.Factory.StartNew(async () => {
                            await this.Capture(args.Subject);
                            await Task.Delay(300);
                            await this.Capture(args.Subject);
                        })
                    .ReportAsWarning();
        }

        void Capture() {
            this.win32WindowFactory
                .ForEachTopLevel(async window => {
                    try {
                        if (this.win32WindowFactory.DisplayInSwitchToList(window))
                            await this.Capture(window);
                    } catch (Exception e) {
                        e.ReportAsWarning();
                    }
                })
                .ReportAsWarning();
        }

        async Task Capture([NotNull] IAppWindow window) {
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            try {
                Rect bounds = window.Bounds;

                TimeSpan retryDelay = TimeSpan.FromMilliseconds(500);
                int retryAttempts = 5;
                while (retryAttempts > 0) {
                    if (window.IsMinimized || !window.IsVisible
                                           || !window.IsResizable || bounds.IsEmpty
                                           || !window.IsOnCurrentDesktop
                                           || !window.CanMove
                                           || string.IsNullOrEmpty(window.Title)) {
                        await Task.Delay(retryDelay).ConfigureAwait(false);
                        retryDelay = new TimeSpan(retryDelay.Ticks * 2);
                    } else
                        break;

                    retryAttempts--;
                }

                if (retryAttempts == 0)
                    return;

                await Task.Factory.StartNew(async () => {
                    if (this.layoutManager.GetLocation(window, searchSuspended: true) != null)
                        return;

                    await Task.WhenAll(this.layouts.ScreenLayouts.Active().Select(l => Layout.GetReady(l.Layout)));

                    Zone targetZone = this.layouts.ScreenLayouts.Active()
                        .SelectMany(layout => layout.Zones.Final())
                        .OrderBy(zone => LocationError(bounds, zone))
                        .FirstOrDefault();

                    var targetBounds = targetZone?.TryGetPhysicalBounds();
                    if (targetBounds != null) {
                        this.layoutManager.Move(window, targetZone);
                        Debug.WriteLine($"move {window.Title} to {targetZone.GetPhysicalBounds()}");
                    }
                }, CancellationToken.None, TaskCreationOptions.None, this.taskScheduler).Unwrap();
            } catch (WindowNotFoundException) { } catch (OperationCanceledException) { }
        }

        async void OnWindowActivated(object sender, WindowEventArgs e) {
            // HACK: track foreground windows to see if they need to be captured
            // needed because OnWindowAppeared in unreliable for cloacked windows
            // see https://stackoverflow.com/questions/32149880/how-to-identify-windows-10-background-store-processes-that-have-non-displayed-wi
            IAppWindow foreground = this.win32WindowFactory.Foreground;
            if (foreground != null)
                await Task.Run(() => this.Capture(foreground)).ConfigureAwait(false);
        }

        static double LocationError(Rect bounds, [NotNull] Zone zone) {
            if (zone == null) throw new ArgumentNullException(nameof(zone));

            var zoneBounds = zone.GetPhysicalBounds();
            return bounds.Corners().Zip(zoneBounds.Corners(),
                (from, to) => from.Diff(to).LengthSquared())
                .Sum();
        }

        public void Dispose() {
            this.layoutManager.WindowAppeared -= this.OnWindowAppeared;
            this.layoutManager.DesktopSwitched -= this.OnDesktopSwitched;
            this.layouts.LayoutLoaded -= this.OnLayoutLoaded;
            this.activationHook.Activated -= this.OnWindowActivated;
        }
    }
}
