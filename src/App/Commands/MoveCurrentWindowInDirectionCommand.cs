﻿namespace LostTech.Stack.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.Linq;
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
        [NotNull] readonly ICollection<ScreenLayout> screenLayouts;
        [NotNull] readonly LayoutManager layoutManager;
        [NotNull] readonly Win32WindowFactory win32WindowFactory;

        public MoveCurrentWindowInDirectionCommand(
            [NotNull] ICollection<ScreenLayout> screenLayouts,
            [NotNull] LayoutManager layoutManager,
            [NotNull] KeyboardMoveBehaviorSettings settings,
            [NotNull] IEnumerable<WindowGroup> windowGroups, [NotNull] Win32WindowFactory win32WindowFactory)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.windowGroups = windowGroups ?? throw new ArgumentNullException(nameof(windowGroups));
            this.win32WindowFactory = win32WindowFactory ?? throw new ArgumentNullException(nameof(win32WindowFactory));
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

            var win32Window = this.win32WindowFactory.Create(window);
            if (win32Window.Equals(this.win32WindowFactory.Desktop)
                || win32Window.Equals(this.win32WindowFactory.Shell))
                return false;

            var bounds = win32Window.Bounds;
            if (bounds.IsEmpty || bounds.Inflated(-1, -1).IsEmpty)
                return false;

            if (this.settings.WindowGroupIgnoreList.Contains(this.windowGroups, window)) {
                Debug.WriteLine("won't move: ignore list");
                return false;
            }

            return true;
        }

        void ICommand.Execute(object parameter) => this.MoveCurrentWindow((PointF)parameter);

        public bool Execute(PointF direction) => this.MoveCurrentWindow(direction);

#pragma warning disable CS0067
        public event EventHandler CanExecuteChanged;
#pragma warning restore CS0067

        const float Epsilon = 2;
        const float LargeValue = 1e9f;

        bool MoveCurrentWindow(PointF direction)
        {
            var windowHandle = User32.GetForegroundWindow();
            if (!this.CanExecute(windowHandle))
                return false;

            Win32Window window = this.win32WindowFactory.Create(windowHandle);
            var windowCenter = window.Bounds.Center();
            var allZones = this.screenLayouts.Active().SelectMany(screen => screen.Zones)
                .Where(zone => zone.Target == null || zone.Equals(zone.Target))
                .ToArray();

            // when moving in the opposite direction enumeration order must be reversed
            bool inverse = direction.X + direction.Y < 0;
            if (inverse)
                Array.Reverse(allZones);

            var sameCenter = allZones.Where(zone => zone.GetPhysicalBounds().Center()
                .Equals(windowCenter, maxDistance: Epsilon)).ToArray();

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
            var directionalInfinity = direction.Scale(LargeValue);
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

            Rank GetRank(Zone zone) =>
                new Rank {
                    IntersectionPercentage =
                        zone.GetPhysicalBounds().Intersection(strip).Area().AtLeast(1)
                        / zone.GetPhysicalBounds().Area().AtLeast(1),

                    CenterTravelDistance = windowCenter.Subtract(zone.GetPhysicalBounds().Center()).Length(),

                    DistanceAlongDirection = DistanceAlongDirection(windowCenter, zone.GetPhysicalBounds().Center(), direction),
                };

            next = next
                   // if there are no zones with the same directional coordinate, continue along it
                   ?? allZones.Where(zone =>
                           zone.TryGetPhysicalBounds()?.IntersectsWith(strip) == true
                           && !sameCenter.Contains(zone)
                           && DistanceAlongDirection(windowCenter, zone.GetPhysicalBounds().Center(), direction) > Epsilon)
                       .OrderBy(GetRank)
                       .FirstOrDefault();

#if DEBUG
            var targets = allZones.Where(zone =>
                    zone.GetPhysicalBounds().IntersectsWith(strip)
                    && !sameCenter.Contains(zone)
                    && DistanceAlongDirection(windowCenter, zone.GetPhysicalBounds().Center(), direction) > Epsilon)
                .ToArray();
            Debug.WriteLine("potential targets:");
            foreach (var zone in targets) {
                Debug.WriteLine($"[{GetRank(zone)}]{zone.GetPhysicalBounds()},");
            }
            Debug.WriteLine("");
#endif
            if (next != null)
                this.layoutManager.Move(window, next).Wait();
            else
                Debug.WriteLine($"nowhere to move {window.Title}");

            return true;
        }

        struct Rank: IComparable<Rank>
        {
            public double IntersectionPercentage { get; set; }
            public double CenterTravelDistance { get; set; }
            public double DistanceAlongDirection { get; set; }

            public double Total =>
                this.CenterTravelDistance * this.DistanceAlongDirection / this.IntersectionPercentage;

            public override string ToString() => $"{this.Total:F0} i:{this.IntersectionPercentage*100:F0}% c:{this.CenterTravelDistance:F0} d:{this.DistanceAlongDirection:F0}";
            public int CompareTo(Rank other) => this.Total.CompareTo(other.Total);
        }

        static double DistanceAlongDirection(PointF @from, PointF to, PointF direction) => to.Subtract(@from).DotProduct(direction);
    }
}
