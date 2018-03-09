namespace LostTech.Stack
{
    using System;
    using System.Windows;

    static class PixelTools
    {
        public static Rect GetPhysicalBounds(this FrameworkElement element) =>
            element.GetPhysicalBounds(element.ActualSize());

        static Rect GetPhysicalBounds(this FrameworkElement element, Size size) {
            if (element == null)
                throw new ArgumentNullException(nameof(element));
            var topLeft = element.PointToScreen(new Point());
            var bottomRight = element.PointToScreen(new Point(size.Width, size.Height));
            return new Rect(topLeft, bottomRight);
        }

        static Rect? TryGetPhysicalBounds(this FrameworkElement element, Size size) {
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            if (PresentationSource.FromVisual(element) == null)
                return null;
            var topLeft = element.PointToScreen(new Point());
            var bottomRight = element.PointToScreen(new Point(size.Width, size.Height));
            return new Rect(topLeft, bottomRight);
        }
        public static Rect? TryGetPhysicalBounds(this FrameworkElement element) =>
            element.TryGetPhysicalBounds(ActualSize(element));

        public static Size ActualSize(this FrameworkElement element) => new Size(element.ActualWidth, element.ActualHeight);
    }
}
