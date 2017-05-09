namespace LostTech.Stack.Behavior
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Forms;
    using System.Windows.Input;
    using Gma.System.MouseKeyHook;
    using JetBrains.Annotations;
    using LostTech.App;
    using LostTech.App.Input;
    using LostTech.Stack.Commands;
    using LostTech.Stack.Models;
    using LostTech.Stack.Settings;
    using LostTech.Stack.Zones;
    using KeyEventArgs = System.Windows.Forms.KeyEventArgs;

    class KeyboardArrowBehavior : IDisposable
    {
        readonly KeyboardMoveBehaviorSettings settings;
        readonly IEnumerable<WindowGroup> windowGroups;
        readonly IKeyboardEvents hook;
        readonly ICollection<ScreenLayout> screenLayouts;
        readonly Action<IntPtr, Zone> move;
        readonly IEnumerable<CommandKeyBinding> keyBindings;

        public KeyboardArrowBehavior(IKeyboardEvents keyboardHook, ICollection<ScreenLayout> screenLayouts,
            IEnumerable<CommandKeyBinding> keyBindings,
            [NotNull] KeyboardMoveBehaviorSettings settings,
            [NotNull] IEnumerable<WindowGroup> windowGroups,
            Action<IntPtr, Zone> move)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.windowGroups = windowGroups ?? throw new ArgumentNullException(nameof(windowGroups));
            this.hook = keyboardHook ?? throw new ArgumentNullException(nameof(keyboardHook));
            this.screenLayouts = screenLayouts ?? throw new ArgumentNullException(nameof(screenLayouts));
            this.move = move ?? throw new ArgumentNullException(nameof(move));
            this.keyBindings = (keyBindings ?? throw new ArgumentNullException(nameof(keyBindings)))
                .Where(binding => Commands.All.Contains(binding.CommandName) && binding.Shortcut != null)
                .ToArray();

            this.hook.KeyDown += this.OnKeyDown;
        }

        async void OnKeyDown(object sender, KeyEventArgs args)
        {
            ModifierKeys modifiers = GetKeyboardModifiers();
            Key key = KeyInterop.KeyFromVirtualKey((int)args.KeyData);
            var stroke = new KeyStroke(key, modifiers);
            CommandKeyBinding binding = this.keyBindings.FirstOrDefault(b => b.Shortcut.Equals(stroke));
            if (binding == null)
                return;

            if (!Directions.TryGetValue(binding.CommandName, out var direction))
                return;

            var moveCommand = new MoveCurrentWindowInDirectionCommand(this.move, this.screenLayouts,
                this.settings, this.windowGroups);
            args.Handled = moveCommand.CanExecute(direction);

            // avoid freezing the system
            await Task.Yield();
            moveCommand.Execute(direction);
        }

        static readonly SortedList<string, Vector> Directions = new SortedList<string, Vector>
        {
            [Commands.MoveLeft] = new Vector(-1, 0),
            [Commands.MoveRight] = new Vector(1, 0),
            [Commands.MoveUp] = new Vector(0, -1),
            [Commands.MoveDown] = new Vector(0, 1),
        };
        static ModifierKeys GetKeyboardModifiers()
            => Keyboard.Modifiers | (IsWinDown() ? ModifierKeys.Windows : ModifierKeys.None);

        static bool IsWinDown() => Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin);

        public void Dispose() { this.hook.KeyDown -= this.OnKeyDown; }

        public static class Commands
        {
            public const string MoveUp = "Move window up";
            public const string MoveDown = "Move window down";
            public const string MoveLeft = "Move window left";
            public const string MoveRight = "Move window right";

            public static readonly IEnumerable<string> All = new []{MoveUp, MoveDown, MoveLeft, MoveRight};
        }
    }
}
