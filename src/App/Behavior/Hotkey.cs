namespace LostTech.Stack.Behavior {
    using System.Collections.Concurrent;
    using System.Windows;
    using LostTech.App.Input;
    using LostTech.Stack.Zones;

    public static class Hotkey
    {
        internal static readonly ConcurrentDictionary<KeyStroke, Zone> moveHotkeys = new ConcurrentDictionary<KeyStroke, Zone>();

        public static KeyStroke GetMoveTo(DependencyObject target) => (KeyStroke)target.GetValue(MoveToProperty);
        public static void SetMoveTo(DependencyObject target, KeyStroke keys) => target.SetValue(MoveToProperty, keys);

        public static readonly DependencyProperty MoveToProperty =
            DependencyProperty.RegisterAttached("MoveTo", typeof(KeyStroke), typeof(Zone),
                new PropertyMetadata(null, propertyChangedCallback: (d, e) => OnHotkeyChanged(d,e,moveHotkeys)));

        static void OnHotkeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e,
            ConcurrentDictionary<KeyStroke,Zone> keyMap) {
            var old = (KeyStroke)e.OldValue;
            if (old != null && keyMap.TryGetValue(old, out var oldZone) && oldZone == d) {
                if (keyMap.TryRemove((KeyStroke)e.OldValue, out oldZone)) {
                    if (oldZone != d)
                        keyMap.TryAdd((KeyStroke)e.OldValue, oldZone);
                }
            }

            var @new = (KeyStroke)e.NewValue;
            if (@new != null)
                keyMap[@new] = (Zone)d;
        }
    }
}
