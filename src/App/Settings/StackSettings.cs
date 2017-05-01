namespace LostTech.Stack.Settings
{
    using LostTech.Stack.DataBinding;
    using LostTech.Stack.Models;

    class StackSettings
    {
        public ScreenLayouts LayoutMap { get; set; } = new ScreenLayouts();
        public Behaviors Behaviors { get; set; } = new Behaviors();
        public NotificationSettings Notifications { get; set; } = new NotificationSettings();
        public CopyableObservableCollection<WindowGroup> WindowGroups { get; set; } =
            new CopyableObservableCollection<WindowGroup>();
    }
}
