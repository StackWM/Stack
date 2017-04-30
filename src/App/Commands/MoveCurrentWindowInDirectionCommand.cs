namespace LostTech.Stack.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Input;
    using JetBrains.Annotations;
    using LostTech.Stack.Utils;
    using LostTech.Stack.Zones;
    using PInvoke;

    class MoveCurrentWindowInDirectionCommand : ICommand
    {
        readonly Action<IntPtr, Zone> move;
        readonly ICollection<ScreenLayout> screenLayouts;

        public MoveCurrentWindowInDirectionCommand([NotNull] Action<IntPtr, Zone> move, [NotNull] ICollection<ScreenLayout> screenLayouts)
        {
            this.move = move ?? throw new ArgumentNullException(nameof(move));
            this.screenLayouts = screenLayouts ?? throw new ArgumentNullException(nameof(screenLayouts));
        }

        public bool CanExecute(object parameter)
        {
            IntPtr window = User32.GetForegroundWindow();
            return Win32.GetWindowInfo(window, out var _);
        }

        void ICommand.Execute(object parameter) => this.MoveCurrentWindow((Vector)parameter);

        public bool Execute(Vector direction) => this.MoveCurrentWindow(direction);

        public event EventHandler CanExecuteChanged;

        const double Epsilon = 2;

        bool MoveCurrentWindow(Vector direction)
        {
            var window = User32.GetForegroundWindow();
            if (!Win32.GetWindowInfo(window, out var info) || User32.IsIconic(window))
                return false;

            var windowBounds = new Rect(info.rcWindow.left, info.rcWindow.top,
                info.rcWindow.right - info.rcWindow.left,
                info.rcWindow.bottom - info.rcWindow.top);
            var windowCenter = windowBounds.Center();
            var allZones = this.screenLayouts.Active().SelectMany(screen => screen.Zones)
                .Where(zone => zone.Target == null || zone.Equals(zone.Target))
                .ToArray();
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
                : sameCenter.SkipWhile(zone => !ReferenceEquals(zone, currentZone))
                    .FirstOrDefault(zone => !ReferenceEquals(zone, currentZone));

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
                this.move(window, next);
            return true;
        }

        static double DistanceAlongDirection(Point @from, Point to, Vector direction) => (to - @from).DotProduct(direction);
    }
}
