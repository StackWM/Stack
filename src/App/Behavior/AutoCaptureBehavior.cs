namespace LostTech.Stack.Behavior
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using JetBrains.Annotations;
    using LostTech.Stack.Models;
    using LostTech.Stack.Settings;
    using LostTech.Stack.Utils;
    using LostTech.Stack.Zones;

    class AutoCaptureBehavior: IDisposable
    {
        readonly GeneralBehaviorSettings settings;
        readonly LayoutManager layoutManager;
        readonly ICollection<ScreenLayout> screenLayouts;
        readonly Win32WindowFactory win32WindowFactory;

        public AutoCaptureBehavior(
            [NotNull] GeneralBehaviorSettings settings,
            [NotNull] LayoutManager layoutManager,
            [NotNull] ICollection<ScreenLayout> screenLayouts,
            [NotNull] Win32WindowFactory win32WindowFactory)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.layoutManager = layoutManager ?? throw new ArgumentNullException(nameof(layoutManager));
            this.screenLayouts = screenLayouts ?? throw new ArgumentNullException(nameof(screenLayouts));
            this.win32WindowFactory = win32WindowFactory ?? throw new ArgumentNullException(nameof(win32WindowFactory));
            this.BeginInitAsync();
        }

        async void BeginInitAsync() {
            await Task.WhenAll(this.screenLayouts.Active().Select(layout => layout.Ready));

            await Task.Yield();

            if (this.settings.CaptureOnStackStart)
                this.Capture();

            this.layoutManager.WindowAppeared += this.OnWindowAppeared;
            this.layoutManager.DesktopSwitched += this.OnDesktopSwitched;
        }

        void OnDesktopSwitched(object sender, EventArgs e) {
            if (this.settings.CaptureOnDesktopSwitch)
                this.Capture();
        }

        void OnWindowAppeared(object sender, EventArgs<IAppWindow> e) {
            if (this.settings.CaptureOnAppStart)
                this.Capture(e.Subject);
        }

        void Capture() {
            this.win32WindowFactory
                .ForEachTopLevel(window => {
                    if (this.win32WindowFactory.DisplayInSwitchToList(window))
                        this.Capture(window);
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

            Zone targetZone = this.screenLayouts.Active()
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
        }
    }
}
