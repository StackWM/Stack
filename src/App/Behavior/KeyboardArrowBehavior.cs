namespace LostTech.Stack.Behavior
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Forms;
    using System.Windows.Input;
    using Gma.System.MouseKeyHook;
    using LostTech.Stack.Utils;
    using LostTech.Stack.Zones;
    using PInvoke;
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
                args.Handled = MoveCurrentWindow(direction);
                return;
            }
        }

        struct WINDOWINFO
        {
            uint cbSize;
            public RECT rcWindow;
            public RECT rcClient;
            uint dwStyle;
            uint dwExStyle;
            uint dwWindowStatus;
            uint cxWindowBorders;
            uint cyWindowBorders;
            ushort atomWindowType;
            uint wCreatorVersion;
        }

        [DllImport("User32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowInfo(IntPtr hwnd, out WINDOWINFO pwi);


        const double Epsilon = 2;

        private bool MoveCurrentWindow(Vector direction)
        {
            var window = User32.GetForegroundWindow();
            if (!GetWindowInfo(window, out var info) || User32.IsIconic(window))
                return false;

            var windowBounds = new Rect(info.rcWindow.left, info.rcWindow.top,
                info.rcWindow.right - info.rcWindow.left,
                info.rcWindow.bottom - info.rcWindow.top);
            var windowCenter = windowBounds.Center();
            var allZones = this.screenLayouts.SelectMany(screen => screen.Zones)
                .Where(zone => zone.Target == null || zone.Equals(zone.Target));
            var sameCenter = allZones.Where(zone => zone.GetPhysicalBounds().Center()
                                            .Equals(windowCenter, epsilon: Epsilon)).ToArray();
            // it only affects cases when centers match
            bool inverse = direction.X + direction.Y < 0;
            if (inverse)
                Array.Reverse(sameCenter);

            var reducedWindowBounds = windowBounds;
            // if inflating rectangle leads to negative size,
            // rectangle is replaced with Rectangle.Empty, which is severely broken for our purposes
            if (windowBounds.Width >= 1 && windowBounds.Height >= 1)
                reducedWindowBounds.Inflate(-1, -1);
            var currentZone = sameCenter.FirstOrDefault(zone => windowBounds.Contains(zone.GetPhysicalBounds())
                                                             && zone.GetPhysicalBounds().Contains(reducedWindowBounds));

            var next = currentZone == null
                    ? sameCenter.FirstOrDefault()
                    : sameCenter.SkipWhile(zone => zone != currentZone)
                                .FirstOrDefault(zone => zone != currentZone);

            var strip = reducedWindowBounds;
            var directionalInfinity = direction * 1e120;
            strip.Inflate(Math.Abs(directionalInfinity.X), Math.Abs(directionalInfinity.Y));

            next = next
                // there are no zones with the same center
                ?? allZones.Where(zone =>
                        zone.GetPhysicalBounds().IntersectsWith(strip)
                        && DistanceAlongDirection(windowCenter, zone.GetPhysicalBounds().Center(), direction) > 0)
                    .OrderBy(zone => DistanceAlongDirection(windowCenter, zone.GetPhysicalBounds().Center(), direction))
                    .FirstOrDefault();

            if (next != null)
                move(window, next);
            return true;
        }

        static double DistanceAlongDirection(Point @from, Point to, Vector direction) => (to - @from).DotProduct(direction);

        static ModifierKeys GetKeyboardModifiers()
            => Keyboard.Modifiers | (IsWinDown() ? ModifierKeys.Windows : ModifierKeys.None);

        static bool IsWinDown() => Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin);

        public void Dispose() { this.hook.KeyDown -= this.OnKeyDown; }
    }
}
