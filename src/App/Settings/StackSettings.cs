namespace LostTech.Stack.Settings
{
    class StackSettings
    {
        public ScreenLayouts LayoutMap { get; set; } = new ScreenLayouts();
        public Behaviors Behaviors { get; set; } = new Behaviors();
        public NotificationSettings Notifications { get; set; } = new NotificationSettings();
    }
}
