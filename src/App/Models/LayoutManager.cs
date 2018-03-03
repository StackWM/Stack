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
    using LostTech.Stack.Zones;

    class LayoutManager : IDisposable
    {
        readonly ICollection<ScreenLayout> screenLayouts;
        readonly Dictionary<IAppWindow, Zone> locations = new Dictionary<IAppWindow, Zone>();
        readonly TaskScheduler taskScheduler;

        public LayoutManager([NotNull] ICollection<ScreenLayout> screenLayouts) {
            this.screenLayouts = screenLayouts ?? throw new ArgumentNullException(nameof(screenLayouts));
            ApplicationWatcher.OnApplicationWindowChange += this.OnApplicationWindowChange;
            this.taskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
        }

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
                    this.StartOnParentThread(() => existedAt.Windows.Remove(window));
                }
                Debug.WriteLine($"Disappeared: {app.AppTitle} traked: {wasTracked}");
                return;
            }

            Debug.WriteLine($"Appeared: {app.AppTitle} from {app.AppName}, {app.AppPath}");
            // TODO: determine if window appeared in an existing zone, and if it needs to be moved
            this.locations.Add(window, null);

            if (app.AppTitle == "Windows PowerShell") {
                this.StartOnParentThread(() => {
                    var toolZone = this.screenLayouts
                        .SelectMany(layout => layout.Zones)
                        .FirstOrDefault(zone => zone.Id != null && zone.Id.StartsWith("Tools"));
                    if (toolZone != null) {
                        this.Move(window, toolZone);
                        Debug.WriteLine("Did it!");
                    }
                });
            }
        }

        void StartOnParentThread(Action action) => Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, this.taskScheduler);

        public void Dispose() {
            ApplicationWatcher.OnApplicationWindowChange -= this.OnApplicationWindowChange;
        }
    }
}
