namespace LostTech.Stack.Behavior
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Forms;
    using System.Windows.Input;
    using Gma.System.MouseKeyHook;
    using LostTech.Stack.Commands;
    using LostTech.Stack.Zones;
    using KeyEventArgs = System.Windows.Forms.KeyEventArgs;

    class KeyboardArrowBehavior : IDisposable
    {
        readonly IKeyboardEvents hook;
        readonly ICollection<ScreenLayout> screenLayouts;
        readonly Action<IntPtr, Zone> move;
        static readonly SortedList<Keys, Vector> Directions = new SortedList<Keys, Vector> {
            [Keys.Left] = new Vector(-1, 0),
            [Keys.Right] = new Vector(1, 0),
            [Keys.Up] = new Vector(0,-1),
            [Keys.Down] = new Vector(0, 1),
        };

        public KeyboardArrowBehavior(IKeyboardEvents keyboardHook, ICollection<ScreenLayout> screenLayouts,
            Action<IntPtr, Zone> move)
        {
            this.hook = keyboardHook ?? throw new ArgumentNullException(nameof(keyboardHook));
            this.screenLayouts = screenLayouts ?? throw new ArgumentNullException(nameof(screenLayouts));
            this.move = move ?? throw new ArgumentNullException(nameof(move));

            this.hook.KeyDown += this.OnKeyDown;
        }

        private void OnKeyDown(object sender, KeyEventArgs args)
        {
            if (GetKeyboardModifiers() == ModifierKeys.Windows
                && Directions.TryGetValue(args.KeyData, out var direction)) {
                var moveCommand = new MoveCurrentWindowInDirectionCommand(this.move, this.screenLayouts);
                args.Handled = moveCommand.Execute(direction);
                return;
            }
        }

        static ModifierKeys GetKeyboardModifiers()
            => Keyboard.Modifiers | (IsWinDown() ? ModifierKeys.Windows : ModifierKeys.None);

        static bool IsWinDown() => Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin);

        public void Dispose() { this.hook.KeyDown -= this.OnKeyDown; }
    }
}
