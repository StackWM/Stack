namespace LostTech.Stack.Models
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using WindowsDesktop;
    using EventHook;
    using JetBrains.Annotations;
    using LostTech.Stack.Utils;
    using LostTech.Stack.Zones;

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

            if (this.locations.TryGetValue(window, out var previousZone) && previousZone != null)
                previousZone.Windows.Remove(window);

            target.Windows.Insert(0, window);
            this.locations[window] = target;
        }

        void OnApplicationWindowChange(object sender, ApplicationEventArgs applicationEventArgs)
        {
            var app = applicationEventArgs.ApplicationData;
            var window = this.windowFactory.Create(app.HWnd);
            if (applicationEventArgs.Event != ApplicationEvents.Launched) {
                this.StartOnParentThread(() => this.RemoveFromSuspended(window));
                bool wasTracked = this.locations.TryGetValue(window, out var existedAt);
                if (wasTracked) {
                    this.locations.Remove(window);
                    this.StartOnParentThread(() => existedAt?.Windows.Remove(window));
                }
                Debug.WriteLine($"Disappeared: {app.AppTitle} traked: {wasTracked}");
                return;
            }

            Debug.WriteLine($"Appeared: {app.AppTitle} from {app.AppName}, {app.AppPath}");
            // TODO: determine if window appeared in an existing zone, and if it needs to be moved
            this.locations.Add(window, null);
            if (VirtualDesktop.IsSupported)
                Debug.WriteLineIf(VirtualDesktop.FromHwnd(app.HWnd) != VirtualDesktop.Current,
                    $"Window {app.AppTitle} appeared on inactive desktop");

#if DEBUG
            this.DecideInitialZone(app)
                .ContinueWith(zone => {
                    if (zone.Result != null) {
                        this.Move(window, zone.Result);
                        Debug.WriteLine("Did it!");
                    }
                }, this.taskScheduler);
#endif
        }

        Task<Zone> DecideInitialZone(WindowData window) {
            if (window.AppTitle == "Windows PowerShell")
                return this.GetZoneByID("Tools");

            if (window.AppTitle?.Contains("Visual Studio Code") == true)
                return this.GetZoneByID("Main");

            return Task.FromResult<Zone>(null);
        }

        #region Virtual Desktop Support
        void InitVirtualDesktopSupport() {
            VirtualDesktop.CurrentChanged += this.VirtualDesktopOnCurrentChanged;
        }
        void DisposeVirtualDesktopSupport() {
            VirtualDesktop.CurrentChanged -= this.VirtualDesktopOnCurrentChanged;
        }
        readonly Dictionary<VirtualDesktop, Dictionary<Zone, List<IAppWindow>>> suspended =
            new Dictionary<VirtualDesktop, Dictionary<Zone, List<IAppWindow>>>();
        void VirtualDesktopOnCurrentChanged(object sender, VirtualDesktopChangedEventArgs change) {
            this.StartOnParentThread(() => {
                var oldWindows = this.suspended.GetOrCreate(change.OldDesktop);
                oldWindows.Clear();

                var activeZones = this.locations.Values.Distinct();
                foreach (Zone activeZone in activeZones) {
                    var zoneSuspendList = oldWindows.GetOrCreate(activeZone);
                    zoneSuspendList.Clear();

                    foreach (IAppWindow appWindow in activeZone.Windows.ToArray()) {
                        if (appWindow is Win32Window window) {
                            if (!VirtualDesktop.IsPinnedWindow(window.Handle)) {
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

                if (this.suspended.TryGetValue(change.NewDesktop, out var newWindows)) {
                    foreach (var zoneContent in newWindows)
                        zoneContent.Key.Windows.AddRange(zoneContent.Value);
                }
            });
        }

        void RemoveFromSuspended(IAppWindow window) {
            foreach (var desktopCollection in this.suspended.Values) {
                foreach (var zoneCollection in desktopCollection.Values) {
                    zoneCollection.Remove(window);
                }
            }
        }
        #endregion

        private Task<Zone> GetZoneByID(string layoutID) => this.StartOnParentThread(() => this.screenLayouts
                                .SelectMany(layout => layout.Zones)
                                .FirstOrDefault(zone => zone.Id != null && zone.Id.StartsWith(layoutID))
                        );
        void StartOnParentThread(Action action) => Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, this.taskScheduler);
        Task<T> StartOnParentThread<T>(Func<T> action) => Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, this.taskScheduler);

        public void Dispose() {
            ApplicationWatcher.OnApplicationWindowChange -= this.OnApplicationWindowChange;
            if (VirtualDesktop.IsSupported)
                this.DisposeVirtualDesktopSupport();
        }
    }
}
