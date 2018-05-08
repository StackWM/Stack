namespace LostTech.Stack.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using WindowsDesktop;
    using EventHook;
    using JetBrains.Annotations;
    using LostTech.Stack.Utils;
    using LostTech.Stack.ViewModels;
    using LostTech.Stack.Zones;
    using Microsoft.HockeyApp;

    class LayoutManager : IDisposable
    {
        readonly ICollection<ScreenLayout> screenLayouts;
        readonly Dictionary<IAppWindow, Zone> locations = new Dictionary<IAppWindow, Zone>();
        readonly TaskScheduler taskScheduler;
        readonly Win32WindowFactory windowFactory;

        public LayoutManager([NotNull] ICollection<ScreenLayout> screenLayouts,
            [NotNull] Win32WindowFactory windowFactory) {
            this.screenLayouts = screenLayouts ?? throw new ArgumentNullException(nameof(screenLayouts));
            this.windowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));
            ApplicationWatcher.OnApplicationWindowChange += this.OnApplicationWindowChange;
            this.taskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

            if (VirtualDesktop.IsSupported)
                this.InitVirtualDesktopSupport();
        }

        public Zone GetLocation([NotNull] IAppWindow window) =>
            this.locations.TryGetValue(
                window ?? throw new ArgumentNullException(nameof(window)),
                out Zone zone)
                ? zone : null;

        public void Move([NotNull] IAppWindow window, [NotNull] Zone target) {
            if (window == null)
                throw new ArgumentNullException(nameof(window));
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            var appWindow = new AppWindowViewModel(window);
            appWindow.PropertyChanged += this.AppWindowOnPropertyChanged;

            if (this.locations.TryGetValue(window, out var previousZone) && previousZone != null) {
                AppWindowViewModel existingWindow = previousZone.Windows.FirstOrDefault(w => w.Equals(appWindow));
                existingWindow?.Dispose();
                previousZone.Windows.Remove(appWindow);
            }

            target.Windows.Insert(0, appWindow);
            this.locations[window] = target;
        }

        void AppWindowOnPropertyChanged(object sender, PropertyChangedEventArgs e) {
            var window = (AppWindowViewModel)sender;
            var underlyingWindow = (Win32Window)window.Window;
            Guid? newDesktop = window.DesktopID;
            switch (e.PropertyName) {
            case nameof(AppWindowViewModel.DesktopID) when VirtualDesktop.IsSupported:
                bool nowVisible = IsPinnedWindow(underlyingWindow.Handle) || VirtualDesktopHelper.IsCurrentVirtualDesktop(underlyingWindow.Handle);
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
                } else if (currentZone != null && newDesktop != null) {
                    // window is moved to another desktop, same zone
                    currentZone.Windows.Remove(window);
                    var targetList = this.suspended.GetOrCreate(newDesktop).GetOrCreate(currentZone);
                    if (!targetList.Contains(window))
                        targetList.Add(window);
                }
                break;
            }
        }

        void ReportTaskException(Task task) {
            if (task.IsFaulted)
                foreach (var exception in task.Exception.InnerExceptions)
                    HockeyClient.Current.TrackException(exception);
        }

        void OnApplicationWindowChange(object sender, ApplicationEventArgs applicationEventArgs)
        {
            var app = applicationEventArgs.ApplicationData;
            var window = this.windowFactory.Create(app.HWnd);
            if (applicationEventArgs.Event != ApplicationEvents.Launched) {
                this.StartOnParentThread(() => this.RemoveFromSuspended(window, dispose: true))
                    .ContinueWith(this.ReportTaskException);
                bool wasTracked = this.locations.TryGetValue(window, out var existedAt);
                if (wasTracked) {
                    this.locations.Remove(window);
                    this.StartOnParentThread(() => {
                        var existing = existedAt?.Windows.FirstOrDefault(vm => vm.Window.Equals(window));
                        existing?.Dispose();
                        return existedAt?.Windows.Remove(existing);
                    }).ContinueWith(this.ReportTaskException);
                }
                Debug.WriteLine($"Disappeared: {app.AppTitle} traked: {wasTracked}");
                return;
            }

            Debug.WriteLine($"Appeared: {app.AppTitle} from {app.AppName}, {app.AppPath}");
            // TODO: determine if window appeared in an existing zone, and if it needs to be moved
            this.locations.Add(window, null);
            if (VirtualDesktop.IsSupported)
                Debug.WriteLineIf(!VirtualDesktopHelper.IsCurrentVirtualDesktop(app.HWnd),
                    $"Window {app.AppTitle} appeared on inactive desktop");

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

                    foreach (AppWindowViewModel appWindow in activeZone.Windows.ToArray()) {
                        if (appWindow.Window is Win32Window window) {
                            if (!IsPinnedWindow(window.Handle)) {
                                zoneSuspendList.Add(appWindow);
                                Debug.WriteLine($"suspended layout of: {appWindow.Title}");
                                activeZone.Windows.Remove(appWindow);
                            } else {
                                Debug.WriteLine($"ignoring pinned window: {appWindow.Title}");
                            }
                        } else
                            throw new NotSupportedException();
                    }
                }

                if (this.suspended.TryGetValue(change.NewDesktop?.Id, out var newWindows)) {
                    foreach (var zoneContent in newWindows)
                        zoneContent.Key.Windows.AddRange(zoneContent.Value);
                }
            }).ConfigureAwait(false);
        }

        static bool IsPinnedWindow(IntPtr hwnd) {
            try {
                return VirtualDesktop.IsPinnedWindow(hwnd);
            } catch (COMException e) {
                e.ReportAsWarning();
                return false;
            } catch (Win32Exception e) {
                e.ReportAsWarning();
                return false;
            } catch (ArgumentException e) {
                e.ReportAsWarning();
                return false;
            }
        }

        [CanBeNull]
        Zone RemoveFromSuspended([CanBeNull] IAppWindow window, bool dispose) {
            Zone zone = null;
            foreach (var desktopCollection in this.suspended.Values) {
                foreach (var zoneCollection in desktopCollection) {
                    AppWindowViewModel existing = zoneCollection.Value.FirstOrDefault(vm => vm.Window.Equals(window));
                    if (dispose)
                        existing?.Dispose();
                    if (existing != null)
                        zoneCollection.Value.Remove(existing);
                    zone = zoneCollection.Key;
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
            ApplicationWatcher.OnApplicationWindowChange -= this.OnApplicationWindowChange;
            if (VirtualDesktop.IsSupported)
                this.DisposeVirtualDesktopSupport();
        }
    }
}
