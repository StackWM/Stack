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
        readonly ConcurrentDictionary<IAppWindow, bool> alreadyCatpured = new ConcurrentDictionary<IAppWindow, bool>();

        public AutoCaptureBehavior(
            [NotNull] IKeyboardEvents keyboardHook,
            [NotNull] IEnumerable<CommandKeyBinding> keyBindings,
            [NotNull] GeneralBehaviorSettings settings,
            [NotNull] LayoutManager layoutManager,
            [NotNull] ILayoutsViewModel layouts,
            [NotNull] Win32WindowFactory win32WindowFactory)
            : base(keyboardHook, keyBindings)
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

                            if (this.win32WindowFactory.DisplayInSwitchToList(window))
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
                            await this.Capture(args.Subject);
                            await Task.Delay(millisecondsDelay: 300);
                            await this.Capture(args.Subject);
                        })
                    .ReportAsWarning();
        }

        void OnWindowDestroyed(object sender, EventArgs<IAppWindow> e) {
            this.alreadyCatpured.TryRemove(e.Subject, out bool _);
        }

        void Capture() {
            this.win32WindowFactory
                .ForEachTopLevel(async window => {
                    try {
                        if (this.win32WindowFactory.DisplayInSwitchToList(window))
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
                this.alreadyCatpured[window] = true;
                return;
            }

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

                await Task.Factory.StartNew(() =>
                    Retry.TimesAsync(attempts: 5, async final => {
                        try {
                            await Task.WhenAll(this.layouts.ScreenLayouts.Active()
                                .Select(l => Layout.GetReady(l.Layout)));
                        } catch (ArgumentNullException e) {
                            if (final) {
                                Debug.WriteLine($"gave up capturing {window.Title} - layouts are not ready");
                                return;
                            }

                            throw new RetriableException(e);
                        }

                        Zone targetZone;
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

                        var targetBounds = targetZone?.TryGetPhysicalBounds();
                        if (targetBounds != null) {
                            this.layoutManager.Move(window, targetZone);
                            Debug.WriteLine($"move {window.Title} to {targetZone.GetPhysicalBounds()}");
                            this.alreadyCatpured[window] = true;
                        }
                    })
                , CancellationToken.None, TaskCreationOptions.None, this.taskScheduler);
            } catch (WindowNotFoundException) { } catch (OperationCanceledException) { }
        }

        async void OnWindowActivated(object sender, WindowEventArgs e) {
            // HACK: track foreground windows to see if they need to be captured
            // needed because OnWindowAppeared in unreliable for cloacked windows
            // see https://stackoverflow.com/questions/32149880/how-to-identify-windows-10-background-store-processes-that-have-non-displayed-wi
            if (this.settings.CaptureOnAppStart.GetValueOrDefault(false) != true)
                return;

            IAppWindow foreground = this.win32WindowFactory.Foreground;
            if (foreground != null && !this.alreadyCatpured.ContainsKey(foreground))
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
