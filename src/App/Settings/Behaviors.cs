namespace LostTech.Stack.Settings
{
    using System.Collections.ObjectModel;
    using LostTech.App;
    using LostTech.Stack.DataBinding;

    public sealed class Behaviors: NotifyPropertyChangedBase, ICopyable<Behaviors>
    {
        public KeyboardMoveBehaviorSettings KeyboardMove { get; set; } = new KeyboardMoveBehaviorSettings();

        public ObservableCollection<CommandKeyBinding> KeyBindings { get; set; } =
            new ObservableCollection<CommandKeyBinding>();

        public Behaviors Copy() => new Behaviors {
            KeyboardMove = this.KeyboardMove.Copy(),
        };
    }
}