namespace LostTech.Stack
{
    using System;
    using System.Windows;
    using System.Drawing;
    using LostTech.Stack.WindowManagement;
    using WPoint = System.Windows.Point;
    using Rect = System.Drawing.RectangleF;
    using Size = System.Drawing.SizeF;

    static class PixelTools
    {
        public static Rect GetPhysicalBounds(this FrameworkElement element) =>
            element.GetPhysicalBounds(element.ActualSize());

        static Rect GetPhysicalBounds(this FrameworkElement element, Size size) {
            if (element == null)
                throw new ArgumentNullException(nameof(element));
            var topLeft = element.PointToScreen(new WPoint()).ToDrawingPoint();
            var bottomRight = element.PointToScreen(new WPoint(size.Width, size.Height)).ToDrawingPoint();
            return new Rect(topLeft, new Size(bottomRight.Subtract(topLeft)));
        }

        static Rect? TryGetPhysicalBounds(this FrameworkElement element, Size size) {
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            if (PresentationSource.FromVisual(element) == null)
                return null;
            var topLeft = element.PointToScreen(new WPoint()).ToDrawingPoint();
            var bottomRight = element.PointToScreen(new WPoint(size.Width, size.Height)).ToDrawingPoint();
            return new Rect(topLeft, new Size(bottomRight.Subtract(topLeft)));
        }
        public static Rect? TryGetPhysicalBounds(this FrameworkElement element) =>
            element.TryGetPhysicalBounds(ActualSize(element));

        public static Size ActualSize(this FrameworkElement element) => new Size((float)element.ActualWidth, (float)element.ActualHeight);
    }
}
