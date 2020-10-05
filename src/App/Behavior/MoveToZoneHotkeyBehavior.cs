namespace LostTech.Stack.Behavior {
    using System;
    using System.ComponentModel;
    using Gma.System.MouseKeyHook;
    using JetBrains.Annotations;
    using LostTech.App.Input;
    using LostTech.Stack.Models;
    using LostTech.Stack.WindowManagement;

    class MoveToZoneHotkeyBehavior: GlobalHotkeyBehaviorBase
    {
        readonly LayoutManager layoutManager;
        readonly Win32WindowFactory windowFactory;

        public MoveToZoneHotkeyBehavior([NotNull] IKeyboardEvents keyboardHook, [NotNull] LayoutManager layoutManager,
            [NotNull] Win32WindowFactory windowFactory)
            : base(keyboardHook) {
            this.layoutManager = layoutManager ?? throw new ArgumentNullException(nameof(layoutManager));
            this.windowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));
        }

        protected override async void OnKeyDown(KeyStroke stroke, HandledEventArgs @event) {
            if (!Hotkey.moveHotkeys.TryGetValue(stroke, out var targetZone) || targetZone == null)
                return;

            if (!targetZone.TryGetTarget(out var target) || !target.IsLoaded)
                return;

            var window = this.windowFactory.Foreground;
            if (window == null)
                return;

            @event.Handled = true;
            await this.layoutManager.Move(window, target);
        }
    }
}
