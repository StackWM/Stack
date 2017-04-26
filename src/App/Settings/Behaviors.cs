namespace LostTech.Stack.Settings
{
    using LostTech.App;
    using LostTech.Stack.DataBinding;

    public sealed class Behaviors: NotifyPropertyChangedBase, ICopyable<Behaviors>
    {
        public KeyboardMoveBehaviorSettings KeyboardMove { get; set; } = new KeyboardMoveBehaviorSettings();
        public MouseMoveBehaviorSettings MouseMove { get; set; } = new MouseMoveBehaviorSettings();

        public Behaviors Copy() => new Behaviors {
            KeyboardMove = this.KeyboardMove.Copy(),
            MouseMove = this.MouseMove.Copy(),
        };
    }
}