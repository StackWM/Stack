namespace LostTech.Stack.Behavior
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Gma.System.MouseKeyHook;
    using JetBrains.Annotations;
    using LostTech.App;
    using LostTech.Stack.ViewModels;

    sealed class HotkeyBehavior: GlobalHotkeyBehaviorBase
    {
        readonly ILayoutsViewModel layoutsViewModel;

        public HotkeyBehavior(
            [NotNull] IKeyboardEvents keyboardHook,
            [NotNull] IEnumerable<CommandKeyBinding> keyBindings,
            [NotNull] ILayoutsViewModel layoutsViewModel)
            : base(keyboardHook, keyBindings) {
            this.layoutsViewModel = layoutsViewModel ?? throw new ArgumentNullException(nameof(layoutsViewModel));
        }

        protected override bool CanExecute(string commandName) {
            switch (commandName) {
            case Commands.ReloadLayouts: return true;
            default: return false;
            }
        }
        protected override async Task ExecuteCommand(string commandName) {
            switch (commandName) {
            case Commands.ReloadLayouts:
                Debug.WriteLine("reloading all layouts");
                var reloadTasks = this.layoutsViewModel.ScreenLayouts.Active()
                    .Select(this.layoutsViewModel.ReloadLayout)
                    .ToArray();
                await Task.WhenAll(reloadTasks);
                break;
            }
        }

        protected override bool IsCommandSupported(string commandName) => Commands.All.Contains(commandName);

        public static class Commands
        {
            public const string ReloadLayouts = "Reload Layouts";

            public static readonly IEnumerable<string> All = new[] { ReloadLayouts };
        }
    }
}
