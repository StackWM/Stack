namespace LostTech.Stack.Behavior
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using EventHook.Hooks;
    using Gma.System.MouseKeyHook;
    using JetBrains.Annotations;
    using LostTech.App;
    using LostTech.Stack.Models;
    using LostTech.Stack.Settings;
    using LostTech.Stack.Utils;
    using LostTech.Stack.ViewModels;
    using LostTech.Stack.WindowManagement;
    using LostTech.Stack.Zones;
    using Rect = System.Drawing.RectangleF;

    sealed class AutoCaptureBehavior: GlobalCommandBehaviorBase
    {
        readonly GeneralBehaviorSettings settings;
        readonly LayoutManager layoutManager;
        readonly ILayoutsViewModel layouts;
        readonly Win32WindowFactory win32WindowFactory;
        readonly WindowHookEx activationHook = WindowHookExFactory.Instance.GetHook();
        readonly TaskScheduler taskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
        readonly IEnumerable<WindowGroup> windowGroups;
        readonly ConcurrentDictionary<IAppWindow, bool> alreadyCaptured = new();
        readonly IReadOnlySet<IAppWindow> excluded;

        public AutoCaptureBehavior(
            [NotNull] IKeyboardEvents keyboardHook,
            [NotNull] IEnumerable<CommandKeyBinding> keyBindings,
            [NotNull] IEnumerable<WindowGroup> windowGroups,
            [NotNull] GeneralBehaviorSettings settings,
            [NotNull] LayoutManager layoutManager,
            [NotNull] ILayoutsViewModel layouts,
            [NotNull] Win32WindowFactory win32WindowFactory,
            IReadOnlySet<IAppWindow> excluded)
            : base(keyboardHook, keyBindings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.layoutManager = layoutManager ?? throw new ArgumentNullException(nameof(layoutManager));
            this.windowGroups = windowGroups ?? throw new ArgumentNullException(nameof(windowGroups));
            this.layouts = layouts ?? throw new ArgumentNullException(nameof(layouts));
            this.win32WindowFactory = win32WindowFactory ?? throw new ArgumentNullException(nameof(win32WindowFactory));
            this.excluded = excluded ?? throw new ArgumentNullException(nameof(excluded));
            this.BeginInitAsync();
        }

        async void BeginInitAsync() {
            await Task.WhenAll(this.layouts.ScreenLayouts.Active().Select(layout => layout.Ready));

            await Task.Yield();

            if (this.settings.CaptureOnStackStart == true)
                this.Capture();

            this.layoutManager.WindowAppeared += this.OnWindowAppeared;
            this.layoutManager.WindowDestroyed += this.OnWindowDestroyed;
            this.layoutManager.DesktopSwitched += this.OnDesktopSwitched;
            this.layouts.LayoutLoaded += this.OnLayoutLoaded;
            this.activationHook.Activated += this.OnWindowActivated;
        }

        async void OnLayoutLoaded(object sender, EventArgs<ScreenLayout> args) {
            if (this.settings.CaptureOnLayoutChange.GetValueOrDefault(false) != true)
                return;

            FrameworkElement layout = args.Subject?.Layout;
            if (layout == null)
                return;

            await Task.Yield();

            try {
                await Layout.GetReady(layout);
            } catch (OperationCanceledException) {
                return;
            }

            Rect? bounds = layout.TryGetPhysicalBounds();
            if (bounds == null)
                // don't autocapture, if layout is immediately dropped
                return;

            await Task.Factory.StartNew(() =>
                this.win32WindowFactory
                    .ForEachTopLevel(async window => {
                        try {
                            Rect intersection = window.Bounds.Intersection(bounds.Value);
                            if (intersection.IsEmpty || intersection.Width < 10 || intersection.Height < 10)
                                return;

                            if (window.IsVisibleInAppSwitcher
                                && this.EligibleForAutoCapture(window))
                                await this.Capture(window);
                        } catch (WindowNotFoundException) { }
                        catch (Exception e) {
                            e.ReportAsWarning();
                        }
                    })
                    ?.ReportAsWarning()).ConfigureAwait(false);
        }

        void OnDesktopSwitched(object sender, EventArgs e) {
            if (this.settings.CaptureOnDesktopSwitch == true)
                Task.Factory.StartNew(this.Capture).ReportAsWarning();
        }

        void OnWindowAppeared(object sender, EventArgs<IAppWindow> args) {
            if (this.settings.CaptureOnAppStart == true)
                Task.Factory.StartNew(async () => {
                            if (!this.EligibleForAutoCapture((Win32Window)args.Subject))
                                return;
                            await this.Capture(args.Subject);
                            await Task.Delay(millisecondsDelay: 300);
                            await this.Capture(args.Subject);
                        })
                    .ReportAsWarning();
        }

        void OnWindowDestroyed(object sender, EventArgs<IAppWindow> e) {
            this.alreadyCaptured.TryRemove(e.Subject, out bool _);
        }

        bool EligibleForAutoCapture(Win32Window window) {
            lock(this.excluded)
                if (this.excluded.Contains(window))
                    return false;

            return !this.settings.CaptureIgnoreList.Contains(this.windowGroups, window.Handle);
        }

        void Capture() {
            this.win32WindowFactory
                .ForEachTopLevel(async window => {
                    try {
                        if (window.IsVisibleInAppSwitcher
                            && this.EligibleForAutoCapture(window))
                            await this.Capture(window);
                    } catch (WindowNotFoundException) { }
                    catch (Exception e) {
                        e.ReportAsWarning();
                    }
                })
                ?.ReportAsWarning();
        }

        protected override bool CanExecute(string commandName) {
            switch (commandName) {
            case Commands.CaptureAll:
                return true;
            default: return false;
            }
        }
        protected override async Task ExecuteCommand(string commandName) {
            switch (commandName) {
            case Commands.CaptureAll:
                await Task.Factory.StartNew(this.Capture).ConfigureAwait(false);
                return;
            }
        }

        async Task Capture([NotNull] IAppWindow window) {
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            if (this.layoutManager.GetLocation(window, searchSuspended: true) != null) {
                this.alreadyCaptured[window] = true;
                return;
            }

            if (await this.CaptureByZoneFilter((Win32Window)window) != false)
                return;

            await this.CaptureToNearest(window);
        }

        async Task<bool?> CaptureByZoneFilter([NotNull] Win32Window window) {
            try {
                var t = await Task.Factory.StartNew(() =>
                    Retry.TimesAsync(attempts: 5, async final => {
                        bool layoutsReady = await this.layouts.ScreenLayouts.Active()
                            .AllReady(Retry.Timeout(TimeSpan.FromSeconds(5)));
                        if (!layoutsReady) {
                            Debug.WriteLine($"gave up capturing {window.Title} - layouts are not ready");
                            return (bool?)null;
                        }

                        Zone? targetZone;
                        try {
                            targetZone = this.layouts.ScreenLayouts.Active()
                               .SelectMany(layout => layout.Zones.Final())
                               .SelectMany(zone => AutoCapture.GetCaptureFilters(zone).OrEmpty().Select(fc => (zone, fc)))
                               .OrderBy(zone => Math.Min(zone.fc.Priority, AutoCapture.GetPriority(zone.fc)))
                               .FirstOrDefault(zone => zone.fc.Filters.Any(f => f.Matches(window.Handle)))
                               .zone;
                        } catch (InvalidOperationException e) {
                            if (final) {
                                Debug.WriteLine($"gave up capturing {window.Title} - zones are not ready");
                                return (bool?)null;
                            }

                            throw new RetriableException(e);
                        }

                        if (targetZone is not null) {
                            Debug.WriteLine($"CaptureByZoneFilter: {window.Title} to {targetZone.Name ?? targetZone.Id}");
                            await this.Capture(window, targetZone);
                            return true;
                        }
                        return false;
                    })
                    , CancellationToken.None, TaskCreationOptions.None, this.taskScheduler);
                return await t;
            } catch (WindowNotFoundException) { } catch (OperationCanceledException) { }
            return null;
        }

        async Task CaptureToNearest([NotNull] IAppWindow window) {
            try {
                Rect bounds = Rect.Empty;

                TimeSpan retryDelay = TimeSpan.FromMilliseconds(500);
                int retryAttempts = 5;
                while (retryAttempts > 0) {
                    try {
                        if (window.IsMinimized || !window.IsVisible
                                               || !window.IsResizable
                                               || !window.CanMove
                                               || string.IsNullOrEmpty(window.Title)
                                               || (bounds = await window.GetBounds()).IsEmpty
                                               || !window.IsOnCurrentDesktop
                        ) {
                            await Task.Delay(retryDelay).ConfigureAwait(false);
                            retryDelay = new TimeSpan(retryDelay.Ticks * 2);
                        } else
                            break;
                    } catch (ShellUnresponsiveException) {
                        retryDelay = TimeSpan.FromTicks(retryDelay.Ticks * 2);
                    }

                    retryAttempts--;
                }

                if (retryAttempts == 0)
                    return;

                var t = await Task.Factory.StartNew(() =>
                    Retry.TimesAsync(attempts: 5, async final => {
                        bool layoutsReady = await this.layouts.ScreenLayouts.Active().AllReady(Retry.Timeout(TimeSpan.FromSeconds(5)));
                        if (!layoutsReady) {
                            if (final) {
                                Debug.WriteLine($"gave up capturing {window.Title} - layouts are not ready");
                                return;
                            }

                            throw new RetriableException("layouts are not ready");
                        }

                        Zone? targetZone;
                        try {
                            targetZone = this.layouts.ScreenLayouts.Active()
                               .SelectMany(layout => layout.Zones.Final())
                               .MinByOrDefault(zone => LocationError(bounds, zone));
                        } catch (InvalidOperationException e) {
                            if (final) {
                                Debug.WriteLine($"gave up capturing {window.Title} - zones are not ready");
                                return;
                            }

                            throw new RetriableException(e);
                        }

                        await this.Capture(window, targetZone);
                    })
                , CancellationToken.None, TaskCreationOptions.None, this.taskScheduler);

                await t;
            } catch (WindowNotFoundException) { } catch (OperationCanceledException) { }
        }

        async Task Capture(IAppWindow window, Zone? targetZone) {
            var targetBounds = targetZone?.TryGetPhysicalBounds();
            if (targetBounds != null) {
                await this.layoutManager.Move(window, targetZone!);
                Debug.WriteLine($"move {window.Title} to {targetBounds.Value}");
                this.alreadyCaptured[window] = true;
            }
        }

        async void OnWindowActivated(object sender, WindowEventArgs e) {
            // HACK: track foreground windows to see if they need to be captured
            // needed because OnWindowAppeared in unreliable for cloacked windows
            // see https://stackoverflow.com/questions/32149880/how-to-identify-windows-10-background-store-processes-that-have-non-displayed-wi
            if (this.settings.CaptureOnAppStart.GetValueOrDefault(false) != true)
                return;

            IAppWindow foreground = this.win32WindowFactory.Foreground;
            if (foreground != null && !this.alreadyCaptured.ContainsKey(foreground)
                && this.EligibleForAutoCapture((Win32Window)foreground))
                await Task.Run(() => this.Capture(foreground)).ConfigureAwait(false);
        }

        static double LocationError(Rect bounds, [NotNull] Zone zone) {
            if (zone == null) throw new ArgumentNullException(nameof(zone));

            var zoneBounds = zone.GetPhysicalBounds();
            return bounds.Corners().Zip(zoneBounds.Corners(),
                (from, to) => from.Subtract(to).LengthSquared())
                .Sum();
        }

        public override void Dispose() {
            this.layoutManager.WindowAppeared -= this.OnWindowAppeared;
            this.layoutManager.DesktopSwitched -= this.OnDesktopSwitched;
            this.layouts.LayoutLoaded -= this.OnLayoutLoaded;
            this.activationHook.Activated -= this.OnWindowActivated;
            
            base.Dispose();
        }

        protected override bool IsCommandSupported(string commandName) => Commands.All.Contains(commandName);

        public static class Commands
        {
            public const string CaptureAll = "Capture all windows";

            public static readonly IEnumerable<string> All = new[] {CaptureAll};
        }
    }
}
