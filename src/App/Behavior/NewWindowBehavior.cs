namespace LostTech.Stack.Behavior
{
    using System;
    using System.Diagnostics;
    using EventHook;

    class NewWindowBehavior: IDisposable
    {
        public NewWindowBehavior() {
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
