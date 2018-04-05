namespace LostTech.Stack.Behavior
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
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

    sealed class KeyboardArrowBehavior : GlobalHotkeyBehaviorBase
    {
        readonly KeyboardMoveBehaviorSettings settings;
        readonly IEnumerable<WindowGroup> windowGroups;
        readonly ICollection<ScreenLayout> screenLayouts;
        readonly Action<IntPtr, Zone> move;
        readonly LayoutManager layoutManager;
        readonly Win32WindowFactory win32WindowFactory;

        public KeyboardArrowBehavior(IKeyboardEvents keyboardHook, ICollection<ScreenLayout> screenLayouts,
            [NotNull] LayoutManager layoutManager,
            IEnumerable<CommandKeyBinding> keyBindings,
            [NotNull] KeyboardMoveBehaviorSettings settings,
            [NotNull] IEnumerable<WindowGroup> windowGroups,
            Action<IntPtr, Zone> move, [NotNull] Win32WindowFactory win32WindowFactory)
        : base(keyboardHook, keyBindings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.windowGroups = windowGroups ?? throw new ArgumentNullException(nameof(windowGroups));
            this.screenLayouts = screenLayouts ?? throw new ArgumentNullException(nameof(screenLayouts));
            this.layoutManager = layoutManager ?? throw new ArgumentNullException(nameof(layoutManager));
            this.move = move ?? throw new ArgumentNullException(nameof(move));
            this.win32WindowFactory = win32WindowFactory ?? throw new ArgumentNullException(nameof(win32WindowFactory));
        }

        protected override bool CanExecute(string commandName) => this.GetCommandIfExecutable(commandName) != null;
        protected override async Task ExecuteCommand(string commandName) {
            MoveCurrentWindowInDirectionCommand moveCommand = this.GetCommandIfExecutable(commandName);

            // avoid freezing the system
            await Task.Yield();
            moveCommand.Execute(Directions[commandName]);
        }

        MoveCurrentWindowInDirectionCommand GetCommandIfExecutable(string commandName) {
            if (!this.settings.Enabled)
                return null;

            if (!Directions.TryGetValue(commandName, out var direction))
                return null;

            var moveCommand = new MoveCurrentWindowInDirectionCommand(this.move, this.screenLayouts,
                this.layoutManager, this.settings, this.windowGroups,
                this.win32WindowFactory);
            if (!moveCommand.CanExecute(direction))
                return moveCommand;
            return moveCommand;
        }

        protected override bool IsCommandSupported(string commandName) => Commands.All.Contains(commandName);

        static readonly SortedList<string, Vector> Directions = new SortedList<string, Vector>
        {
            [Commands.MoveLeft] = new Vector(-1, 0),
            [Commands.MoveRight] = new Vector(1, 0),
            [Commands.MoveUp] = new Vector(0, -1),
            [Commands.MoveDown] = new Vector(0, 1),
        };

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
