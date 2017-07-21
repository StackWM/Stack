namespace LostTech.Stack.Behavior
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using EventHook;
    using JetBrains.Annotations;

    class NewWindowBehavior: IDisposable
    {
        readonly ICollection<ScreenLayout> screenLayouts;
        public NewWindowBehavior([NotNull] ICollection<ScreenLayout> screenLayouts) {
            this.screenLayouts = screenLayouts ?? throw new ArgumentNullException(nameof(screenLayouts));
            ApplicationWatcher.OnApplicationWindowChange += this.OnApplicationWindowChange;
        }

        void OnApplicationWindowChange(object sender, ApplicationEventArgs applicationEventArgs) {
            var app = applicationEventArgs.ApplicationData;
            if (applicationEventArgs.Event != ApplicationEvents.Launched) {
                Debug.WriteLine($"Disappeared: {app.AppTitle} from {app.AppName}, {app.AppPath}");
                return;
            }

            Debug.WriteLine($"Appeared: {app.AppTitle} from {app.AppName}, {app.AppPath}");
        }

        public void Dispose() {
            ApplicationWatcher.OnApplicationWindowChange -= this.OnApplicationWindowChange;
        }
    }
}
