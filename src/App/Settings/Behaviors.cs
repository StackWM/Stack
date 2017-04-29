namespace LostTech.Stack.Settings
{
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Windows.Input;
    using LostTech.App;
    using LostTech.App.Input;
    using LostTech.Stack.DataBinding;
    using M = System.Windows.Input.ModifierKeys;

    public sealed class Behaviors: NotifyPropertyChangedBase, ICopyable<Behaviors>
    {
        public KeyboardMoveBehaviorSettings KeyboardMove { get; set; } = new KeyboardMoveBehaviorSettings();
        public MouseMoveBehaviorSettings MouseMove { get; set; } = new MouseMoveBehaviorSettings();

        public ObservableCollection<CommandKeyBinding> KeyBindings { get; set; } =
            new ObservableCollection<CommandKeyBinding>();

        public Behaviors Copy() => new Behaviors {
            KeyboardMove = this.KeyboardMove.Copy(),
            MouseMove = this.MouseMove.Copy(),
            KeyBindings = new ObservableCollection<CommandKeyBinding>(this.KeyBindings.Select(CopyableCommandKeyBinding.Copy)),
        };

        public void AddMissingBindings()
        {
            foreach (CommandKeyBinding binding in DefaultKeyBindings) {
                if (this.KeyBindings.Any(b => b.CommandName == binding.CommandName))
                    continue;

                this.KeyBindings.Add(binding.Copy());
            }
        }

        static readonly CommandKeyBinding[] DefaultKeyBindings = {
            new CommandKeyBinding("Move window up", new KeyStroke(Key.Up, M.Windows)),
            new CommandKeyBinding("Move window down", new KeyStroke(Key.Down, M.Windows)),
            new CommandKeyBinding("Move window left", new KeyStroke(Key.Left, M.Windows)),
            new CommandKeyBinding("Move window right", new KeyStroke(Key.Right, M.Windows)),
        };
    }
}