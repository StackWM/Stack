namespace LostTech.Stack
{
    using System;
    using System.Windows;

    static class PixelTools
    {
        public static Rect GetPhysicalBounds(this FrameworkElement element)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));
            var presentationSource = PresentationSource.FromVisual(element);
            var toDevice = presentationSource.CompositionTarget.TransformToDevice;
            var topLeft = element.PointToScreen(new Point());
            var bottomRight = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));
            return new Rect(topLeft, bottomRight);
        }
    }
}
