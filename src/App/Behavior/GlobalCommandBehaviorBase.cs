namespace LostTech.Stack.Behavior {
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading.Tasks;
    using Gma.System.MouseKeyHook;
    using JetBrains.Annotations;
    using LostTech.App;
    using LostTech.App.Input;

    abstract class GlobalCommandBehaviorBase : GlobalHotkeyBehaviorBase
    {
        readonly IKeyboardEvents keyboardHook;
        readonly IEnumerable<CommandKeyBinding> keyBindings;

        protected GlobalCommandBehaviorBase(
            [NotNull] IKeyboardEvents keyboardHook,
            [NotNull] IEnumerable<CommandKeyBinding> keyBindings): base(keyboardHook) {
            this.keyBindings = (keyBindings ?? throw new ArgumentNullException(nameof(keyBindings)))
                .Where(binding => this.IsCommandSupported(binding.CommandName) && binding.Shortcut != null)
                .ToArray();
        }

        protected override async void OnKeyDown(KeyStroke stroke, HandledEventArgs @event) {
            CommandKeyBinding binding = this.keyBindings.FirstOrDefault(b => b.Shortcut.Equals(stroke));
            if (binding == null)
                return;
            @event.Handled = this.CanExecute(binding.CommandName);
            if (!@event.Handled)
                return;
            await this.ExecuteCommand(binding.CommandName).ConfigureAwait(false);
        }

        protected abstract bool CanExecute(string commandName);
        protected abstract Task ExecuteCommand(string commandName);
        protected abstract bool IsCommandSupported(string commandName);
    }
}
