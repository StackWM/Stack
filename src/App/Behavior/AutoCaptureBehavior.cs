namespace LostTech.Stack.Behavior
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using JetBrains.Annotations;
    using LostTech.Stack.Models;
    using LostTech.Stack.Settings;
    using LostTech.Stack.Utils;
    using LostTech.Stack.ViewModels;
    using LostTech.Stack.Zones;

    class AutoCaptureBehavior: IDisposable
    {
        readonly GeneralBehaviorSettings settings;
        readonly LayoutManager layoutManager;
        readonly ILayoutsViewModel layouts;
        readonly Win32WindowFactory win32WindowFactory;
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
        }

        void OnLayoutLoaded(object sender, EventArgs<ScreenLayout> args) {
            if (!this.settings.CaptureOnLayoutChange)
                return;

            if (args.Subject?.Layout == null)
                return;

            Rect bounds = args.Subject.Layout.GetPhysicalBounds();
            this.win32WindowFactory
                .ForEachTopLevel(window => {
                    try {
                        Rect intersection = window.Bounds.Intersection(bounds);
                        if (intersection.IsEmpty || intersection.Width < 10 || intersection.Height < 10)
                            return;

                        if (this.win32WindowFactory.DisplayInSwitchToList(window))
                            this.Capture(window);
                    } catch (Exception e) { e.ReportAsWarning(); }
                })
                .ReportAsWarning();
        }

        void OnDesktopSwitched(object sender, EventArgs e) {
            if (this.settings.CaptureOnDesktopSwitch)
                Task.Factory.StartNew(() =>
                        this.Capture(), CancellationToken.None, TaskCreationOptions.None,
                        this.taskScheduler)
                    .ReportAsWarning();
        }

        void OnWindowAppeared(object sender, EventArgs<IAppWindow> args) {
            if (this.settings.CaptureOnAppStart)
                Task.Factory.StartNew(async () => {
                            this.Capture(args.Subject);
                            await Task.Delay(300);
                            this.Capture(args.Subject);
                        }, CancellationToken.None, TaskCreationOptions.None,
                        this.taskScheduler)
                    .ReportAsWarning();
        }

        void Capture() {
            this.win32WindowFactory
                .ForEachTopLevel(window => {
                    try {
                        if (this.win32WindowFactory.DisplayInSwitchToList(window))
                            this.Capture(window);
                    } catch (Exception e) {
                        e.ReportAsWarning();
                    }
                })
                .ReportAsWarning();
        }

        void Capture([NotNull] IAppWindow window) {
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            Rect bounds = window.Bounds;

            if (window.IsMinimized || !window.IsVisible
                || !window.IsResizable || bounds.IsEmpty
                || string.IsNullOrEmpty(window.Title))
                return;

            if (this.layoutManager.GetLocation(window, searchSuspended: true) != null)
                return;

            Zone targetZone = this.layouts.ScreenLayouts.Active()
                .SelectMany(layout => layout.Zones.Final())
                .OrderBy(zone => LocationError(bounds, zone))
                .FirstOrDefault();

            if (targetZone != null) {
                this.layoutManager.Move(window, targetZone);
                Debug.WriteLine($"move {window.Title} to {targetZone.GetPhysicalBounds()}");
            }
        }

        static double LocationError(Rect bounds, [NotNull] Zone zone) {
            if (zone == null) throw new ArgumentNullException(nameof(zone));

            var zoneBounds = zone.GetPhysicalBounds();
            return bounds.Corners().Zip(zoneBounds.Corners(),
                (from, to) => (from - to).LengthSquared)
                .Sum();
        }

        public void Dispose() {
            this.layoutManager.WindowAppeared -= this.OnWindowAppeared;
            this.layoutManager.DesktopSwitched -= this.OnDesktopSwitched;
            this.layouts.LayoutLoaded -= this.OnLayoutLoaded;
        }
    }
}
