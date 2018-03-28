﻿namespace LostTech.Stack.Commands
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
    using LostTech.Stack.Zones;
    using PInvoke;

    class MoveCurrentWindowInDirectionCommand : ICommand
    {
        [NotNull] readonly KeyboardMoveBehaviorSettings settings;
        [NotNull] readonly IEnumerable<WindowGroup> windowGroups;
        [NotNull] readonly Action<IntPtr, Zone> move;
        [NotNull] readonly ICollection<ScreenLayout> screenLayouts;
        [NotNull] readonly LayoutManager layoutManager;
        [NotNull] readonly Win32WindowFactory win32WindowFactory;

        public MoveCurrentWindowInDirectionCommand([NotNull] Action<IntPtr, Zone> move,
            [NotNull] ICollection<ScreenLayout> screenLayouts,
            [NotNull] LayoutManager layoutManager,
            [NotNull] KeyboardMoveBehaviorSettings settings,
            [NotNull] IEnumerable<WindowGroup> windowGroups, [NotNull] Win32WindowFactory win32WindowFactory)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.windowGroups = windowGroups ?? throw new ArgumentNullException(nameof(windowGroups));
            this.win32WindowFactory = win32WindowFactory ?? throw new ArgumentNullException(nameof(win32WindowFactory));
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
            if (!Win32.GetWindowInfo(window, out var _)) {
                Debug.WriteLine("can't move: window inaccessible");
                return false;
            }

            if (User32.IsIconic(window))
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

        const double Epsilon = 2;
        const double LargeValue = 1e9;

        bool MoveCurrentWindow(Vector direction)
        {
            var windowHandle = User32.GetForegroundWindow();
            if (!this.CanExecute(windowHandle) || !Win32.GetWindowInfo(windowHandle, out var info))
                return false;

            var windowBounds = new Rect(info.rcWindow.left, info.rcWindow.top,
                info.rcWindow.right - info.rcWindow.left,
                info.rcWindow.bottom - info.rcWindow.top);
            var windowCenter = windowBounds.Center();
            var allZones = this.screenLayouts.Active().SelectMany(screen => screen.Zones)
                .Where(zone => zone.Target == null || zone.Equals(zone.Target))
                .ToArray();

            // when moving in the opposite direction enumeration order must be reversed
            bool inverse = direction.X + direction.Y < 0;
            if (inverse)
                Array.Reverse(allZones);

            var sameCenter = allZones.Where(zone => zone.GetPhysicalBounds().Center()
                .Equals(windowCenter, epsilon: Epsilon)).ToArray();

            var reducedWindowBounds = windowBounds.Inflated(-1,-1);
            Win32Window window = this.win32WindowFactory.Create(windowHandle);
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

            next = next
                   // if there are no zones with the same directional coordinate, continue along it
                   ?? allZones.Where(zone =>
                           zone.GetPhysicalBounds().IntersectsWith(strip)
                           && !sameCenter.Contains(zone)
                           && DistanceAlongDirection(windowCenter, zone.GetPhysicalBounds().Center(), direction) > 0)
                       .OrderBy(zone => DistanceAlongDirection(windowCenter, zone.GetPhysicalBounds().Center(), direction)
                            * (windowCenter - zone.GetPhysicalBounds().Center()).Length
                            * zone.GetPhysicalBounds().Area() / zone.GetPhysicalBounds().Intersection(strip).Area())
                       .FirstOrDefault();

#if DEBUG
            var targets = allZones.Where(zone =>
                    zone.GetPhysicalBounds().IntersectsWith(strip)
                    && !sameCenter.Contains(zone)
                    && DistanceAlongDirection(windowCenter, zone.GetPhysicalBounds().Center(), direction) > 0)
                .ToArray();
            Debug.WriteLine("potential targets:");
            foreach (var zone in targets) {
                Debug.Write($"{zone.GetPhysicalBounds()}, ");
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
