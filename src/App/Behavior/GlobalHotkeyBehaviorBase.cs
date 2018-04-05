namespace LostTech.Stack.Behavior
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows.Input;
    using Gma.System.MouseKeyHook;
    using JetBrains.Annotations;
    using LostTech.App;
    using LostTech.App.Input;
    using KeyEventArgs = System.Windows.Forms.KeyEventArgs;

    abstract class GlobalHotkeyBehaviorBase : IDisposable
    {
        readonly IKeyboardEvents keyboardHook;
        readonly IEnumerable<CommandKeyBinding> keyBindings;

        protected GlobalHotkeyBehaviorBase(
            [NotNull] IKeyboardEvents keyboardHook,
            [NotNull] IEnumerable<CommandKeyBinding> keyBindings) {
            this.keyboardHook = keyboardHook ?? throw new ArgumentNullException(nameof(keyboardHook));
            this.keyBindings = (keyBindings ?? throw new ArgumentNullException(nameof(keyBindings)))
                .Where(binding => this.IsCommandSupported(binding.CommandName) && binding.Shortcut != null)
                .ToArray();

            this.keyboardHook.KeyDown += this.OnKeyDown;
        }

        async void OnKeyDown(object sender, KeyEventArgs e) {
            ModifierKeys modifiers = GetKeyboardModifiers();
            Key key = KeyInterop.KeyFromVirtualKey((int)e.KeyData);
            if (key == Key.None)
                key = KeyInterop.KeyFromVirtualKey((int)e.KeyCode);
            var stroke = new KeyStroke(key, modifiers);
            CommandKeyBinding binding = this.keyBindings.FirstOrDefault(b => b.Shortcut.Equals(stroke));
            if (binding == null)
                return;
            e.Handled = this.CanExecute(binding.CommandName);
            if (!e.Handled)
                return;
            await this.ExecuteCommand(binding.CommandName).ConfigureAwait(false);
        }

        protected abstract bool CanExecute(string commandName);
        protected abstract Task ExecuteCommand(string commandName);
        protected abstract bool IsCommandSupported(string commandName);

        static ModifierKeys GetKeyboardModifiers()
            => Keyboard.Modifiers | (IsWinDown() ? ModifierKeys.Windows : ModifierKeys.None);

        static bool IsWinDown() => Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin);

        public virtual void Dispose() => this.keyboardHook.KeyDown -= this.OnKeyDown;
    }
}
