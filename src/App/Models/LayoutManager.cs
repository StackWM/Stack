namespace LostTech.Stack.Models
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using WindowsDesktop;
    using EventHook;
    using JetBrains.Annotations;
    using LostTech.Stack.Utils;
    using LostTech.Stack.ViewModels;
    using LostTech.Stack.WindowManagement;
    using LostTech.Stack.Zones;
    using Microsoft.HockeyApp;
    using RectangleF = System.Drawing.RectangleF;
    using static System.FormattableString;

    class LayoutManager : IDisposable, IWindowTracker, IWindowManager
    {
        readonly ICollection<ScreenLayout> screenLayouts;
        readonly Dictionary<IAppWindow, Zone> locations = new Dictionary<IAppWindow, Zone>();
        readonly Dictionary<IAppWindow, Task<RectangleF>> origins = new Dictionary<IAppWindow, Task<RectangleF>>();
        readonly TaskScheduler taskScheduler;
        readonly Win32WindowFactory windowFactory;
        readonly EventHookFactory eventHookFactory = new EventHookFactory();
        readonly ApplicationWatcher applicationWatcher;

        public LayoutManager([NotNull] ICollection<ScreenLayout> screenLayouts,
            [NotNull] Win32WindowFactory windowFactory) {
            this.screenLayouts = screenLayouts ?? throw new ArgumentNullException(nameof(screenLayouts));
            if (this.screenLayouts is INotifyCollectionChanged trackableLayouts) {
                trackableLayouts.CollectionChanged += this.OnScreenLayoutCollectionChanged;
                this.OnScreenLayoutCollectionChanged(trackableLayouts, new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Replace, newItems: screenLayouts.ToArray(), oldItems: Array.Empty<ScreenLayout>()));
            }

            this.windowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));
            this.applicationWatcher = this.eventHookFactory.GetApplicationWatcher();
            this.applicationWatcher.OnApplicationWindowChange += this.OnApplicationWindowChange;
            this.applicationWatcher.OnWindowOverride += this.ApplicationWatcher_OnWindowOverride;
            this.applicationWatcher.Start();
            this.taskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

            if (VirtualDesktop.HasMinimalSupport)
                this.InitVirtualDesktopSupport();
        }

        void ApplicationWatcher_OnWindowOverride(object sender, WindowOverrideEventArgs e) {
            new InvalidOperationException(
                Invariant($"Received window override for {e.OldWindow.HWnd}->{e.NewWindow.HWnd}.\n") +
                Invariant($" creation time: {e.OldWindow.CreationTime}->{e.NewWindow.CreationTime}\n") +
                $" app: {e.OldWindow.AppName}->{e.NewWindow.AppName}").ReportAsWarning();

            if (e.OldWindow.AppName != e.NewWindow.AppName)
                this.OnApplicationWindowChange(sender, new ApplicationEventArgs {
                    ApplicationData = e.OldWindow,
                    Event = ApplicationEvents.Closed,
                });
        }

        public Zone GetLocation([NotNull] IAppWindow window) {
            lock(this.locations)
                return this.locations.TryGetValue(
                    window ?? throw new ArgumentNullException(nameof(window)),
                    out Zone zone)
                    ? zone
                    : null;
        }

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

            this.StopTrackingInternal(appWindow);

            bool isOnCurrentDesktop;
            try {
                isOnCurrentDesktop = window.IsOnCurrentDesktop;
            } catch (WindowNotFoundException) {
                return;
            }

            if (!this.origins.ContainsKey(window)) {
                try {
                    this.origins[window] = window.GetBounds().IgnoreUnobservedExceptions();
                } catch (WindowNotFoundException) { }
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
                lock (this.locations)
                    this.locations[window] = target;
            }
        }

        public async Task<bool?> Detach([NotNull] IAppWindow window, bool restoreBounds = false) {
            if (window == null) throw new ArgumentNullException(nameof(window));

            bool result = this.StopTrackingInternal(window);
            if (!this.origins.TryGetValue(window, out var originalBounds))
                return result;

            this.origins.Remove(window);
            if (!restoreBounds)
                return result;

            try {
                var bounds = await originalBounds.ConfigureAwait(false);
                if (!bounds.IsEmpty)
                    await window.Move(bounds).ConfigureAwait(false);
            } catch (WindowNotFoundException) { }

            return result;
        }

        void StopTrackingInternal(AppWindowViewModel appWindow) {
            this.RemoveFromSuspended(appWindow.Window, dispose: true);
            Zone previousZone;
            lock (this.locations)
                if (!this.locations.TryGetValue(appWindow.Window, out previousZone) || previousZone == null)
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
                case nameof(AppWindowViewModel.DesktopID) when VirtualDesktop.HasMinimalSupport:
                    bool nowVisible = underlyingWindow.IsOnCurrentDesktop;
                    Zone currentZone;
                    lock (this.locations)
                        this.locations.TryGetValue(underlyingWindow, out currentZone);
                    if (nowVisible) {
                        if (currentZone != null)
                            // great - nothing to do!
                            break;

                        // window moved to the current desktop from somewhere
                        var suspendedInZone = this.RemoveFromSuspended(underlyingWindow, dispose: false);
                        if (suspendedInZone != null) {
                            suspendedInZone.Windows.Add(window);
                            lock(this.locations)
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

        bool StopTrackingInternal(IAppWindow window) {
            this.StartOnParentThread(() => this.RemoveFromSuspended(window, dispose: true))
                .ContinueWith(this.ReportTaskException);
            lock (this.locations) {
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
        }

        void OnWindowClosed(object sender, EventArgs _) {
            var window = (IAppWindow)sender;
            this.StopTrackingInternal(window);
            this.origins.Remove(window);
            this.WindowDestroyed?.Invoke(this, new EventArgs<IAppWindow>(window));
        }

        void OnApplicationWindowChange(object sender, ApplicationEventArgs applicationEventArgs)
        {
            var app = applicationEventArgs.ApplicationData;
            var window = this.windowFactory.Create(app.HWnd);
            if (applicationEventArgs.Event == ApplicationEvents.Closed) {
                bool wasTracked = this.StopTrackingInternal(window);
                this.origins.Remove(window);
                Debug.WriteLine($"Disappeared: {app.AppTitle} traked: {wasTracked}");
                this.WindowDestroyed?.Invoke(this, new EventArgs<IAppWindow>(window));
                return;
            }

            lock (this.locations) {
                if (this.locations.ContainsKey(window))
                    return;

                Debug.WriteLine($"Appeared: {app.AppTitle} from {app.AppName}, {app.AppPath}");
                // TODO: determine if window appeared in an existing zone, and if it needs to be moved
                this.locations.Add(window, null);
            }

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

                IEnumerable<Zone> activeZones;
                lock(this.locations)
                    activeZones = this.locations.Values.Distinct().ToList();

                foreach (Zone activeZone in activeZones) {
                    if (activeZone == null)
                        continue;
                    
                    var zoneSuspendList = oldWindows.GetOrCreate(activeZone);
                    zoneSuspendList.Clear();

                    foreach (AppWindowViewModel appWindow in activeZone.Windows.ToArray())
                        try {
                            if (!appWindow.Window.IsOnCurrentDesktop) {
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

        static readonly DependencyPropertyDescriptor ScreenContentDescriptor = DependencyPropertyDescriptor.FromProperty(
            ContentControl.ContentProperty, typeof(ContentControl));

        void OnScreenLayoutCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            foreach (ScreenLayout layout in e.OldItems ?? Array.Empty<ScreenLayout>())
                ScreenContentDescriptor.RemoveValueChanged(layout, this.OnScreenLayoutContentChanged);
            foreach (ScreenLayout layout in e.NewItems ?? Array.Empty<ScreenLayout>())
                ScreenContentDescriptor.AddValueChanged(layout, this.OnScreenLayoutContentChanged);
        }

        void OnScreenLayoutContentChanged(object sender, EventArgs e) {
            var remove = this.locations
                .Where(location => location.Value != null && Window.GetWindow(location.Value) == null)
                .ToList();
            foreach (var entry in remove)
                this.StopTrackingInternal(entry.Key);
        }

        private Task<Zone> GetZoneByID(string layoutID) => this.StartOnParentThread(() => this.screenLayouts
                                .SelectMany(layout => layout.Zones)
                                .FirstOrDefault(zone => zone.Id != null && zone.Id.StartsWith(layoutID))
                        );
        Task StartOnParentThread(Action action) => Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, this.taskScheduler);
        Task<T> StartOnParentThread<T>(Func<T> action) => Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, this.taskScheduler);

        public void Dispose() {
            if (this.screenLayouts is INotifyCollectionChanged trackableLayouts) {
                trackableLayouts.CollectionChanged -= this.OnScreenLayoutCollectionChanged;
            }
            this.OnScreenLayoutCollectionChanged(this.screenLayouts, new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Replace, newItems: Array.Empty<ScreenLayout>(), oldItems: this.screenLayouts.ToArray()));

            this.applicationWatcher.OnApplicationWindowChange -= this.OnApplicationWindowChange;
            this.applicationWatcher.Stop();
            this.eventHookFactory.Dispose();
            if (VirtualDesktop.HasMinimalSupport)
                this.DisposeVirtualDesktopSupport();

            List<AppWindowViewModel> windows;
            lock (this.locations)
                windows = this.locations.Values
                    .SelectMany(zone => zone?.Windows ?? Enumerable.Empty<AppWindowViewModel>())
                    .Concat(this.suspended.Values.SelectMany(desktopWindows =>
                        desktopWindows.Values.SelectMany(windowList => windowList))).ToList();

            foreach (var window in windows)
                this.StopTrackingInternal(window);
        }

        public event EventHandler<EventArgs<IAppWindow>> WindowAppeared;
        public event EventHandler<EventArgs<IAppWindow>> WindowDestroyed;
        public event EventHandler DesktopSwitched;
    }
}
