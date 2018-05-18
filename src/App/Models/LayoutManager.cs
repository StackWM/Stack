﻿namespace LostTech.Stack.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using WindowsDesktop;
    using EventHook;
    using JetBrains.Annotations;
    using LostTech.Stack.Utils;
    using LostTech.Stack.ViewModels;
    using LostTech.Stack.Zones;
    using Microsoft.HockeyApp;

    class LayoutManager : IDisposable, IWindowTracker
    {
        readonly ICollection<ScreenLayout> screenLayouts;
        readonly Dictionary<IAppWindow, Zone> locations = new Dictionary<IAppWindow, Zone>();
        readonly TaskScheduler taskScheduler;
        readonly Win32WindowFactory windowFactory;
        readonly EventHookFactory eventHookFactory = new EventHookFactory();
        readonly ApplicationWatcher applicationWatcher;

        public LayoutManager([NotNull] ICollection<ScreenLayout> screenLayouts,
            [NotNull] Win32WindowFactory windowFactory) {
            this.screenLayouts = screenLayouts ?? throw new ArgumentNullException(nameof(screenLayouts));
            this.windowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));
            this.applicationWatcher = this.eventHookFactory.GetApplicationWatcher();
            this.applicationWatcher.OnApplicationWindowChange += this.OnApplicationWindowChange;
            this.applicationWatcher.Start();
            this.taskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

            if (VirtualDesktop.IsSupported)
                this.InitVirtualDesktopSupport();
        }

        public Zone GetLocation([NotNull] IAppWindow window) =>
            this.locations.TryGetValue(
                window ?? throw new ArgumentNullException(nameof(window)),
                out Zone zone)
                ? zone : null;

        public Zone GetLocation([NotNull] IAppWindow window, bool searchSuspended) {
            var currentScreenLocation = this.GetLocation(window);
            if (!searchSuspended)
                return currentScreenLocation;

            return currentScreenLocation ?? this.SearchSuspended(window, out var _);
        }

        public void Move([NotNull] IAppWindow window, [NotNull] Zone target) {
            if (window == null)
                throw new ArgumentNullException(nameof(window));
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            var appWindow = new AppWindowViewModel(window);
            appWindow.PropertyChanged += this.AppWindowOnPropertyChanged;
            appWindow.Window.Closed += this.OnWindowClosed;

            this.StopTracking(appWindow);

            bool isOnCurrentDesktop;
            try {
                isOnCurrentDesktop = window.IsOnCurrentDesktop;
            } catch (WindowNotFoundException) {
                return;
            }

            if (!isOnCurrentDesktop) {
                Guid? desktopID = appWindow.DesktopID;
                if (desktopID == null)
                    return;

                var desktopSuspendList = this.suspended.GetOrCreate(desktopID);
                var zoneSuspendList = desktopSuspendList.GetOrCreate(target);
                zoneSuspendList.Add(appWindow);
            } else {
                target.Windows.Insert(0, appWindow);
                this.locations[window] = target;
            }
        }

        void StopTracking(AppWindowViewModel appWindow) {
            this.RemoveFromSuspended(appWindow.Window, dispose: true);
            if (!this.locations.TryGetValue(appWindow.Window, out var previousZone) || previousZone == null)
                return;

            AppWindowViewModel existingWindow = previousZone.Windows.FirstOrDefault(w => w.Equals(appWindow));
            if (existingWindow != null)
                existingWindow.Window.Closed -= this.OnWindowClosed;
            existingWindow?.Dispose();
            previousZone.Windows.Remove(appWindow);
        }

        void AppWindowOnPropertyChanged(object sender, PropertyChangedEventArgs e) {
            var window = (AppWindowViewModel)sender;
            var underlyingWindow = window.Window;
            try {
                Guid? newDesktop = window.DesktopID;
                switch (e.PropertyName) {
                case nameof(AppWindowViewModel.DesktopID) when VirtualDesktop.IsSupported:
                    bool nowVisible = underlyingWindow.IsVisibleOnAllDesktops || underlyingWindow.IsOnCurrentDesktop;
                    this.locations.TryGetValue(underlyingWindow, out var currentZone);
                    if (nowVisible) {
                        if (currentZone != null)
                            // great - nothing to do!
                            break;

                        // window moved to the current desktop from somewhere
                        var suspendedInZone = this.RemoveFromSuspended(underlyingWindow, dispose: false);
                        if (suspendedInZone != null) {
                            suspendedInZone.Windows.Add(window);
                            this.locations[underlyingWindow] = suspendedInZone;
                        }
                        this.WindowAppeared?.Invoke(this, new EventArgs<IAppWindow>(underlyingWindow));
                    } else if (currentZone != null && newDesktop != null) {
                        // window is moved to another desktop, same zone
                        currentZone.Windows.Remove(window);
                        var targetList = this.suspended.GetOrCreate(newDesktop).GetOrCreate(currentZone);
                        if (!targetList.Contains(window))
                            targetList.Add(window);
                    }

                    break;
                }
            } catch (WindowNotFoundException) { }
        }

        void ReportTaskException(Task task) {
            if (task.IsFaulted)
                foreach (var exception in task.Exception.InnerExceptions)
                    HockeyClient.Current.TrackException(exception);
        }

        bool StopTracking(IAppWindow window) {
            this.StartOnParentThread(() => this.RemoveFromSuspended(window, dispose: true))
                .ContinueWith(this.ReportTaskException);
            bool wasTracked = this.locations.TryGetValue(window, out var existedAt);
            if (wasTracked) {
                this.locations.Remove(window);
                this.StartOnParentThread(() => {
                    var existing = existedAt?.Windows.FirstOrDefault(vm => vm.Window.Equals(window));
                    if (existing != null)
                        existing.Window.Closed -= this.OnWindowClosed;
                    existing?.Dispose();
                    return existedAt?.Windows.Remove(existing);
                }).ContinueWith(this.ReportTaskException);
            }

            return wasTracked;
        }

        void OnWindowClosed(object sender, EventArgs _) => this.StopTracking((IAppWindow)sender);

        void OnApplicationWindowChange(object sender, ApplicationEventArgs applicationEventArgs)
        {
            var app = applicationEventArgs.ApplicationData;
            var window = this.windowFactory.Create(app.HWnd);
            if (applicationEventArgs.Event != ApplicationEvents.Launched) {
                bool wasTracked = this.StopTracking(window);
                Debug.WriteLine($"Disappeared: {app.AppTitle} traked: {wasTracked}");
                return;
            }

            if (this.locations.ContainsKey(window))
                return;

            Debug.WriteLine($"Appeared: {app.AppTitle} from {app.AppName}, {app.AppPath}");
            // TODO: determine if window appeared in an existing zone, and if it needs to be moved
            this.locations.Add(window, null);
#if DEBUG
            if (VirtualDesktop.IsSupported)
                try {
                    Debug.WriteLineIf(!this.windowFactory.Create(app.HWnd).IsOnCurrentDesktop,
                        $"Window {app.AppTitle} appeared on inactive desktop");
                } catch (WindowNotFoundException) { }
#endif

#if DEBUG
            this.DecideInitialZone(app)
                .ContinueWith(zoneTask => {
                    if (zoneTask.IsFaulted) {
                        this.ReportTaskException(zoneTask);
                        return;
                    }

                    if (zoneTask.Result != null) {
                        this.Move(window, zoneTask.Result);
                        Debug.WriteLine("Did it!");
                    }
                }, this.taskScheduler);
#endif

            this.WindowAppeared?.Invoke(this, new EventArgs<IAppWindow>(window));
        }

#if DEBUG
        Task<Zone> DecideInitialZone(WindowData window) {
            if (window.AppTitle == "Windows PowerShell")
                return this.GetZoneByID("Tools");

            if (window.AppTitle?.Contains("Visual Studio Code") == true)
                return this.GetZoneByID("Main");

            return Task.FromResult<Zone>(null);
        }
#endif

        #region Virtual Desktop Support
        void InitVirtualDesktopSupport() {
            VirtualDesktop.CurrentChanged += this.VirtualDesktopOnCurrentChanged;
        }
        void DisposeVirtualDesktopSupport() {
            VirtualDesktop.CurrentChanged -= this.VirtualDesktopOnCurrentChanged;
        }
        readonly Dictionary<Guid?, Dictionary<Zone, List<AppWindowViewModel>>> suspended =
            new Dictionary<Guid?, Dictionary<Zone, List<AppWindowViewModel>>>();
        async void VirtualDesktopOnCurrentChanged(object sender, VirtualDesktopChangedEventArgs change) {
            await this.StartOnParentThread(() => {
                var oldWindows = this.suspended.GetOrCreate(change.OldDesktop?.Id);
                oldWindows.Clear();

                var activeZones = this.locations.Values.Distinct();
                foreach (Zone activeZone in activeZones) {
                    if (activeZone == null)
                        continue;
                    
                    var zoneSuspendList = oldWindows.GetOrCreate(activeZone);
                    zoneSuspendList.Clear();

                    foreach (AppWindowViewModel appWindow in activeZone.Windows.ToArray())
                        try {
                            if (!appWindow.Window.IsVisibleOnAllDesktops) {
                                zoneSuspendList.Add(appWindow);
                                Debug.WriteLine($"suspended layout of: {appWindow.Title}");
                                activeZone.Windows.Remove(appWindow);
                            } else {
                                Debug.WriteLine($"ignoring pinned window: {appWindow.Title}");
                            }
                        } catch (WindowNotFoundException) {
                            activeZone.Windows.Remove(appWindow);
                        }
                }

                if (this.suspended.TryGetValue(change.NewDesktop?.Id, out var newWindows)) {
                    foreach (var zoneContent in newWindows)
                        zoneContent.Key.Windows.AddRange(zoneContent.Value);
                }

                this.DesktopSwitched?.Invoke(this, EventArgs.Empty);
            }).ConfigureAwait(false);
        }

        [CanBeNull]
        Zone RemoveFromSuspended([CanBeNull] IAppWindow window, bool dispose) {
            Zone zone = null;
            foreach (var desktopCollection in this.suspended.Values) {
                foreach (var zoneCollection in desktopCollection) {
                    AppWindowViewModel existing = zoneCollection.Value.FirstOrDefault(vm => vm.Window.Equals(window));
                    if (existing == null)
                        continue;

                    if (dispose) {
                        existing.Window.Closed -= this.OnWindowClosed;
                        existing.Dispose();
                    }

                    zoneCollection.Value.Remove(existing);
                    zone = zoneCollection.Key;
                }
            }
            return zone;
        }

        [CanBeNull]
        Zone SearchSuspended([CanBeNull] IAppWindow window, out Guid? desktop) {
            desktop = null;
            Zone zone = null;
            foreach (var desktopCollection in this.suspended)
            {
                foreach (var zoneCollection in desktopCollection.Value)
                {
                    AppWindowViewModel existing = zoneCollection.Value.FirstOrDefault(vm => vm.Window.Equals(window));
                    if (existing != null) {
                        desktop = desktopCollection.Key;
                        zone = zoneCollection.Key;
                    }
                }
            }
            return zone;
        }
#endregion

        private Task<Zone> GetZoneByID(string layoutID) => this.StartOnParentThread(() => this.screenLayouts
                                .SelectMany(layout => layout.Zones)
                                .FirstOrDefault(zone => zone.Id != null && zone.Id.StartsWith(layoutID))
                        );
        Task StartOnParentThread(Action action) => Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, this.taskScheduler);
        Task<T> StartOnParentThread<T>(Func<T> action) => Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, this.taskScheduler);

        public void Dispose() {
            this.applicationWatcher.OnApplicationWindowChange -= this.OnApplicationWindowChange;
            this.applicationWatcher.Stop();
            this.eventHookFactory.Dispose();
            if (VirtualDesktop.IsSupported)
                this.DisposeVirtualDesktopSupport();

            var windows = this.locations.Values
                .SelectMany(zone => zone?.Windows ?? Enumerable.Empty<AppWindowViewModel>())
                .Concat(this.suspended.Values.SelectMany(desktopWindows =>
                    desktopWindows.Values.SelectMany(windowList => windowList))).ToList();

            foreach (var window in windows)
                this.StopTracking(window);
        }

        public event EventHandler<EventArgs<IAppWindow>> WindowAppeared;
        public event EventHandler DesktopSwitched;
    }
}
