namespace LostTech.Stack.Settings
{
    using System.Collections.ObjectModel;
    using LostTech.App;
    using LostTech.Stack.DataBinding;

    public sealed class Behaviors: NotifyPropertyChangedBase, ICopyable<Behaviors>
    {
        public KeyboardMoveBehaviorSettings KeyboardMove { get; set; } = new KeyboardMoveBehaviorSettings();
        public MouseMoveBehaviorSettings MouseMove { get; set; } = new MouseMoveBehaviorSettings();

        public ObservableCollection<CopyableCommandKeyBinding> KeyBindings { get; set; } =
            new ObservableCollection<CopyableCommandKeyBinding>();

        public Behaviors Copy() => new Behaviors {
            KeyboardMove = this.KeyboardMove.Copy(),
            MouseMove = this.MouseMove.Copy(),
        };
    }
}