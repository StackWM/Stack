namespace LostTech.Stack.Models
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using EventHook;
    using JetBrains.Annotations;
    using LostTech.Stack.WindowManagement;
    using LostTech.Stack.Zones;

    class LayoutManager : IDisposable
    {
        readonly ICollection<ScreenLayout> screenLayouts;
        readonly Dictionary<IAppWindow, Zone> locations = new Dictionary<IAppWindow, Zone>();
        readonly TaskScheduler taskScheduler;
        readonly EventHookFactory eventHookFactory = new EventHookFactory();
        readonly ApplicationWatcher applicationWatcher;

        public LayoutManager([NotNull] ICollection<ScreenLayout> screenLayouts) {
            this.screenLayouts = screenLayouts ?? throw new ArgumentNullException(nameof(screenLayouts));
            this.applicationWatcher = this.eventHookFactory.GetApplicationWatcher();
            this.applicationWatcher.OnApplicationWindowChange += this.OnApplicationWindowChange;
            this.applicationWatcher.Start();
            this.taskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
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

            target.Windows.Add(window);
            this.locations[window] = target;
        }

        void OnApplicationWindowChange(object sender, ApplicationEventArgs applicationEventArgs)
        {
            var app = applicationEventArgs.ApplicationData;
            var window = new Win32Window(app.HWnd);
            if (applicationEventArgs.Event != ApplicationEvents.Launched) {
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

        private Task<Zone> GetZoneByID(string layoutID) => this.StartOnParentThread(() => this.screenLayouts
                                .SelectMany(layout => layout.Zones)
                                .FirstOrDefault(zone => zone.Id != null && zone.Id.StartsWith(layoutID))
                        );
        void StartOnParentThread(Action action) => Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, this.taskScheduler);
        Task<T> StartOnParentThread<T>(Func<T> action) => Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, this.taskScheduler);

        public void Dispose() {
            this.applicationWatcher.OnApplicationWindowChange -= this.OnApplicationWindowChange;
            this.applicationWatcher.Stop();
            this.eventHookFactory.Dispose();
        }
    }
}
