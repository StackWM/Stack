namespace LostTech.Stack.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Windows;
    using System.Windows.Input;
    using JetBrains.Annotations;
    using LostTech.Stack.Models;
    using LostTech.Stack.Settings;
    using LostTech.Stack.Utils;
    using LostTech.Stack.WindowManagement;
    using LostTech.Stack.WindowManagement.WinApi;
    using LostTech.Stack.Zones;
    using PInvoke;

    class MoveCurrentWindowInDirectionCommand : ICommand
    {
        [NotNull] readonly KeyboardMoveBehaviorSettings settings;
        [NotNull] readonly IEnumerable<WindowGroup> windowGroups;
        [NotNull] readonly Action<IntPtr, Zone> move;
        [NotNull] readonly ICollection<ScreenLayout> screenLayouts;
        [NotNull] readonly LayoutManager layoutManager;

        public MoveCurrentWindowInDirectionCommand([NotNull] Action<IntPtr, Zone> move,
            [NotNull] ICollection<ScreenLayout> screenLayouts,
            [NotNull] LayoutManager layoutManager,
            [NotNull] KeyboardMoveBehaviorSettings settings,
            [NotNull] IEnumerable<WindowGroup> windowGroups)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.windowGroups = windowGroups ?? throw new ArgumentNullException(nameof(windowGroups));
            this.move = move ?? throw new ArgumentNullException(nameof(move));
            this.screenLayouts = screenLayouts ?? throw new ArgumentNullException(nameof(screenLayouts));
            this.layoutManager = layoutManager ?? throw new ArgumentNullException(nameof(layoutManager));
        }

        public bool CanExecute(object parameter)
        {
            IntPtr window = User32.GetForegroundWindow();
            return this.CanExecute(window);
        }

        public bool CanExecute(IntPtr window)
        {
            if (!this.settings.Enabled)
                return false;

            if (!Win32.GetWindowInfo(window, out var _)) {
                Debug.WriteLine("can't move: window inaccessible");
                return false;
            }

            if (User32.IsIconic(window))
                return false;

            var bounds = new Win32Window(window, suppressSystemMargin: false).Bounds;
            if (bounds.IsEmpty || bounds.Inflated(-1, -1).IsEmpty)
                return false;

            if (this.settings.WindowGroupIgnoreList.Contains(this.windowGroups, window)) {
                Debug.WriteLine("won't move: ignore list");
                return false;
            }

            return true;
        }

        void ICommand.Execute(object parameter) => this.MoveCurrentWindow((Vector)parameter);

        public bool Execute(Vector direction) => this.MoveCurrentWindow(direction);

        public event EventHandler CanExecuteChanged;

        const float Epsilon = 2;
        const float LargeValue = 1e9f;

        bool MoveCurrentWindow(Vector direction)
        {
            var windowHandle = User32.GetForegroundWindow();
            if (!this.CanExecute(windowHandle))
                return false;

            Win32Window window = new Win32Window(windowHandle, suppressSystemMargin: false);
            var windowCenter = window.Bounds.Center();
            var allZones = this.screenLayouts.Active().SelectMany(screen => screen.Zones)
                .Where(zone => zone.Target == null || zone.Equals(zone.Target))
                .ToArray();

            // when moving in the opposite direction enumeration order must be reversed
            bool inverse = direction.X + direction.Y < 0;
            if (inverse)
                Array.Reverse(allZones);

            var sameCenter = allZones.Where(zone => zone.GetPhysicalBounds().Center()
                .Equals(windowCenter, epsilon: Epsilon)).ToArray();

            var reducedWindowBounds = window.Bounds.Inflated(-1,-1);
            if (reducedWindowBounds.IsEmpty)
                return false;

            var currentZone = this.layoutManager.GetLocation(window);

            var next = currentZone == null
                ? sameCenter.FirstOrDefault()
                : sameCenter.SkipWhile(zone => !ReferenceEquals(zone, currentZone))
                    .FirstOrDefault(zone => !ReferenceEquals(zone, currentZone));

            Debug.WriteLineIf(next != null, "going to a zone with the same center");

            var strip = reducedWindowBounds;
            var directionalInfinity = direction * LargeValue;
            if (directionalInfinity.X > 1)
                strip.Width += LargeValue;
            if (directionalInfinity.X < -1) {
                strip.Width += LargeValue;
                strip.X -= LargeValue;
            }

            if (directionalInfinity.Y > 1)
                strip.Height += LargeValue;
            if (directionalInfinity.Y < -1) {
                strip.Height += LargeValue;
                strip.Y -= LargeValue;
            }

            //next = next
            //    // enumerate intersecting zones with the same directional coordinate,
            //    // that follow current zone in the global zone order
            //    ?? allZones.Where(zone =>
            //        zone.GetPhysicalBounds().IntersectsWith(strip)
            //        && DistanceAlongDirection(windowCenter, zone.GetPhysicalBounds().Center(), direction)
            //            .IsBetween(-Epsilon, Epsilon))
            //    .SkipWhile(zone => !ReferenceEquals(zone, currentZone))
            //    .FirstOrDefault(zone => !ReferenceEquals(zone, currentZone));

            Debug.WriteLineIf(next != null, "going to a zone with the same directional coordinate");

            double GetRank(Zone zone) {
                double intersectionPercentage =
                    zone.GetPhysicalBounds().Area().AtLeast(1)
                    / zone.GetPhysicalBounds().Intersection(strip).Area().AtLeast(1);

                double centerTravelDistance = (windowCenter.ToWPF() - zone.GetPhysicalBounds().Center().ToWPF()).Length;

                return DistanceAlongDirection(windowCenter.ToWPF(), zone.GetPhysicalBounds().Center().ToWPF(), direction)
                       * centerTravelDistance
                       * intersectionPercentage;
            }

            next = next
                   // if there are no zones with the same directional coordinate, continue along it
                   ?? allZones.Where(zone =>
                           zone.GetPhysicalBounds().IntersectsWith(strip)
                           && !sameCenter.Contains(zone)
                           && DistanceAlongDirection(windowCenter.ToWPF(), zone.GetPhysicalBounds().Center().ToWPF(), direction) > 0)
                       .OrderBy(GetRank)
                       .FirstOrDefault();

#if DEBUG
            var targets = allZones.Where(zone =>
                    zone.GetPhysicalBounds().IntersectsWith(strip)
                    && !sameCenter.Contains(zone)
                    && DistanceAlongDirection(windowCenter.ToWPF(), zone.GetPhysicalBounds().Center().ToWPF(), direction) > 0)
                .ToArray();
            Debug.WriteLine("potential targets:");
            foreach (var zone in targets) {
                Debug.Write($"[{GetRank(zone)}]{zone.GetPhysicalBounds()},");
            }
            Debug.WriteLine("");
#endif
            if (next != null)
                this.move(windowHandle, next);
            else
                Debug.WriteLine($"nowhere to move {window.Title}");

            return true;
        }

        static double DistanceAlongDirection(Point @from, Point to, Vector direction) => (to - @from).DotProduct(direction);
    }
}
