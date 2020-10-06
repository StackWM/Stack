namespace LostTech.Stack.Behavior
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.Linq;
    using System.Threading.Tasks;
    using Gma.System.MouseKeyHook;
    using JetBrains.Annotations;
    using LostTech.App;
    using LostTech.Stack.Extensibility.Services;
    using LostTech.Stack.ViewModels;
    using LostTech.Stack.WindowManagement;
    using LostTech.Windows;
    using PInvoke;

    sealed class HotkeyBehavior: GlobalCommandBehaviorBase
    {
        readonly ILayoutsViewModel layoutsViewModel;
        readonly ILayoutMappingViewModel layoutMapping;
        readonly Win32WindowFactory windowFactory;
        readonly IScreenProvider screenProvider;
        readonly IWindowManager windowManager;

        public HotkeyBehavior(
            [NotNull] IKeyboardEvents keyboardHook,
            [NotNull] IEnumerable<CommandKeyBinding> keyBindings,
            [NotNull] ILayoutsViewModel layoutsViewModel,
            [NotNull] IScreenProvider screenProvider,
            [NotNull] Win32WindowFactory windowFactory,
            [NotNull] IWindowManager windowManager,
            [NotNull] ILayoutMappingViewModel layoutMapping)
            : base(keyboardHook, keyBindings) {
            this.layoutsViewModel = layoutsViewModel ?? throw new ArgumentNullException(nameof(layoutsViewModel));
            this.screenProvider = screenProvider ?? throw new ArgumentNullException(nameof(screenProvider));
            this.windowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));
            this.windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
            this.layoutMapping = layoutMapping ?? throw new ArgumentNullException(nameof(layoutMapping));
        }

        protected override bool CanExecute(string commandName) {
            switch (commandName) {
            case Commands.ReloadLayouts:
            case Commands.ChooseLayout:
                return true;
            case Commands.DetachWindow:
                return this.windowFactory.Foreground != null;
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
            case Commands.ChooseLayout:
                Debug.WriteLine("suggesting to change layout");
                Win32Screen currentScreen = this.GetCurrentScreen();
                var selector = this.layoutMapping.ShowLayoutSelector(currentScreen);
                selector.Topmost = true;
                break;
            case Commands.DetachWindow:
                var foreground = this.windowFactory.Foreground;
                if (foreground != null)
                    await this.windowManager.Detach(foreground, restoreBounds: true);
                break;
            }
        }

        [NotNull]
        Win32Screen GetCurrentScreen() {
            Win32Window foreground = this.windowFactory.Foreground;
            if (foreground?.Equals(this.windowFactory.Desktop) == true
                || foreground?.Equals(this.windowFactory.Shell) == true)
                foreground = null;
            var activeBounds = foreground?.Bounds;
            if (activeBounds != null && activeBounds.Value.Area() > 10) {
                var activeWindowScreen = this.screenProvider.Screens
                    .Where(s => s.IsActive && s.WorkingArea.IntersectsWith(activeBounds.Value))
                    .OrderByDescending(s => s.WorkingArea.Intersection(activeBounds.Value).Area())
                    .FirstOrDefault();
                if (activeWindowScreen != null)
                    return activeWindowScreen;
            }

            User32.GetCursorPos(out var point);
            var mouseScreen = this.screenProvider.Screens
                .FirstOrDefault(s => s.IsActive && s.WorkingArea.Contains(point.ToDrawingPoint()));
            return mouseScreen ?? this.screenProvider.Screens.First(s => s.IsActive && s.IsPrimary);
        }

        protected override bool IsCommandSupported(string commandName) => Commands.All.Contains(commandName);

        public static class Commands
        {
            public const string ReloadLayouts = "Reload Layouts";
            public const string ChooseLayout = "Select Layout";
            public const string DetachWindow = "Detach window, and restore its bounds";

            public static readonly IEnumerable<string> All = new[] { ReloadLayouts, ChooseLayout, DetachWindow };
        }
    }
}
