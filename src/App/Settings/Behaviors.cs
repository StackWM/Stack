namespace LostTech.Stack.Settings
{
    using LostTech.App;
    using LostTech.Stack.DataBinding;

    public sealed class Behaviors: NotifyPropertyChangedBase, ICopyable<Behaviors>
    {
        public KeyboardMoveBehaviorSettings KeyboardMove { get; set; } = new KeyboardMoveBehaviorSettings();

        public Behaviors Copy() => new Behaviors {
            KeyboardMove = this.KeyboardMove.Copy(),
        };
    }
}