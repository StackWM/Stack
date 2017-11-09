namespace LostTech.Stack.ScreenCoordinates
{
    using System;
    using System.Windows;
    using System.Windows.Media;
    using JetBrains.Annotations;
    using LostTech.Windows;

    static class WindowScreenLayouts
    {
        public static void AdjustToClientArea([NotNull] this Window window, [NotNull] Win32Screen screen) {
            MoveToScreen(window, screen);

            var size = new Vector(screen.WorkingArea.Width, screen.WorkingArea.Height);
            var dimensions = screen.TransformFromDevice.Transform(size);
            window.Width = dimensions.X;
            window.Height = dimensions.Y;
        }

        public static void Center([NotNull] this Window window, [NotNull] Win32Screen screen) {
            MoveToScreen(window, screen);

            var screenSize = new Vector(screen.WorkingArea.Width, screen.WorkingArea.Height);
            Vector windowSize = screen.TransformToDevice.Transform(new Vector(window.Width, window.Height));
            Point topLeft = screen.WorkingArea.TopLeft + (screenSize - windowSize) / 2;
            topLeft = screen.TransformFromDevice.Transform(topLeft);
            window.Left = topLeft.X;
            window.Top = topLeft.Y;
        }

        public static void MoveToScreen([NotNull] Window window, [NotNull] Win32Screen screen) {
            if (screen == null)
                throw new ArgumentNullException(nameof(screen));
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            Point topLeft = screen.TransformFromDevice.Transform(screen.WorkingArea.TopLeft);
            window.Left = topLeft.X;
            window.Top = topLeft.Y;
        }

        /// <summary>
        /// Sets location and size of the window to fit to <see cref="Window.Margin"/> on the specified screen.
        /// Negative margin behavior is undefined.
        /// </summary>
        public static void FitToMargin([NotNull] this Window window, [NotNull] Win32Screen screen) {
            MoveToScreen(window, screen);

            Vector wpfScreenSize = screen.TransformFromDevice.Transform((Vector)screen.WorkingArea.Size);
            double width = wpfScreenSize.X - window.Margin.Left - window.Margin.Right;
            window.Width = Math.Min(width, double.IsNaN(window.MaxWidth) ? width : window.MaxWidth);
            double height = wpfScreenSize.Y - window.Margin.Top - window.Margin.Bottom;
            window.Height = Math.Min(height, double.IsNaN(window.MaxHeight) ? height : window.MaxHeight);
            Vector sizeFix = new Vector(width - window.Width, height - window.Height) / 2;
            Vector marginOffset = new Vector(window.Margin.Left, window.Margin.Top);

            Point topLeft = screen.TransformFromDevice.Transform(screen.WorkingArea.TopLeft)
                            + marginOffset + sizeFix;
            window.Left = topLeft.X;
            window.Top = topLeft.Y;
        }
    }
}
