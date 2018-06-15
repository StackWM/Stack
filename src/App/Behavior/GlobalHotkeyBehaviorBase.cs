namespace LostTech.Stack.Behavior {
    using System;
    using System.ComponentModel;
    using System.Windows.Input;
    using Gma.System.MouseKeyHook;
    using JetBrains.Annotations;
    using LostTech.App.Input;
    using KeyEventArgs = System.Windows.Forms.KeyEventArgs;

    abstract class GlobalHotkeyBehaviorBase : IDisposable {
        readonly IKeyboardEvents keyboardHook;

        protected GlobalHotkeyBehaviorBase(
            [NotNull] IKeyboardEvents keyboardHook) {
            this.keyboardHook = keyboardHook ?? throw new ArgumentNullException(nameof(keyboardHook));
            this.keyboardHook.KeyDown += this.OnKeyDown;
        }

        void OnKeyDown(object sender, KeyEventArgs e) {
            ModifierKeys modifiers = GetKeyboardModifiers();
            Key key = KeyInterop.KeyFromVirtualKey((int)e.KeyData);
            if (key == Key.None)
                key = KeyInterop.KeyFromVirtualKey((int)e.KeyCode);
            var stroke = new KeyStroke(key, modifiers);
            var @event = new HandledEventArgs(e.Handled);
            this.OnKeyDown(stroke, @event);
            e.Handled = @event.Handled;
        }

        protected abstract void OnKeyDown(KeyStroke stroke, HandledEventArgs @event);

        static ModifierKeys GetKeyboardModifiers()
            => Keyboard.Modifiers | (IsWinDown() ? ModifierKeys.Windows : ModifierKeys.None);

        static bool IsWinDown() => Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin);

        public virtual void Dispose() => this.keyboardHook.KeyDown -= this.OnKeyDown;
    }
}
