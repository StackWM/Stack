namespace LostTech.Stack.Compat
{
    using System;
    using System.Runtime.InteropServices;
    using DesktopNotifications;
    using LostTech.App;

    [ClassInterface(ClassInterfaceType.None)]
    [ComSourceInterfaces(typeof(INotificationActivationCallback))]
    [Guid("688D7B93-990E-495C-BC3A-E2DF827B5A25"), ComVisible(true)]
    public class UrlNotificationActivator : NotificationActivator
    {
        public override void OnActivated(string arguments, NotificationUserInput userInput, string appUserModelId) {
            var uri = new Uri(arguments, UriKind.Absolute);
            BoilerplateApp.Boilerplate.Launch(uri);
        }
    }
}
