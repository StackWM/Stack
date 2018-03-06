namespace LostTech.Stack
{
    using System;
    using System.Windows;

    static class PixelTools
    {
        public static Rect GetPhysicalBounds(this FrameworkElement element) =>
            element.GetPhysicalBounds(new Size(element.ActualWidth, element.ActualHeight));

        public static Rect GetPhysicalBounds(this FrameworkElement element, Size size) {
            if (element == null)
                throw new ArgumentNullException(nameof(element));
            var topLeft = element.PointToScreen(new Point());
            var bottomRight = element.PointToScreen(new Point(size.Width, size.Height));
            return new Rect(topLeft, bottomRight);
        }
    }
}
