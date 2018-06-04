namespace LostTech.Stack.ScreenCoordinates
{
    using System;
    using System.Windows;
    using System.Windows.Interop;
    using JetBrains.Annotations;
    using LostTech.Stack.Utils;
    using LostTech.Windows;

    static class WindowScreenLayouts
    {
        public static void AdjustToClientArea([NotNull] this Window window, [NotNull] Win32Screen screen) {
            MoveToScreen(window, screen);

            var size = new Vector(screen.WorkingArea.Width, screen.WorkingArea.Height);
            var dimensions = screen.TransformFromDevice.Transform(size);
            // this is a hack to force layout recompute. InvalidateMeasure does not help
            window.Width = dimensions.X - 1;
            window.Width = dimensions.X;
            window.Height = dimensions.Y;
        }

        public static void MoveToScreen([NotNull] Window window, [NotNull] Win32Screen screen) {
            if (screen == null)
                throw new ArgumentNullException(nameof(screen));
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            var handleSource = (HwndSource)PresentationSource.FromVisual(window);
            double windowToDeviceScale = screen.WindowToDeviceScale(handleSource);
            var topLeft = screen.WorkingArea.TopLeft().Scale(1/(float)windowToDeviceScale);
                //screen.WorkingArea.TopLeft();
            if (topLeft.X > 0) topLeft.X = (int)topLeft.X + 0.5f;
            if (topLeft.Y > 0) topLeft.Y = (int)topLeft.Y + 0.5f;
            window.Left = topLeft.X;
            window.Top = topLeft.Y;
        }

        /// <summary>
        /// Sets location and size of the window to fit to <see cref="Window.Margin"/> on the specified screen.
        /// Negative margin behavior is undefined.
        /// </summary>
        public static void FitToMargin([NotNull] this Window window, [NotNull] Win32Screen screen) {
            MoveToScreen(window, screen);

            Vector wpfScreenSize = screen.TransformFromDevice.Transform(screen.WorkingArea.Size.AsWPFVector());
            double width = wpfScreenSize.X - window.Margin.Left - window.Margin.Right;
            window.Width = Math.Min(width, double.IsNaN(window.MaxWidth) ? width : window.MaxWidth);
            double height = wpfScreenSize.Y - window.Margin.Top - window.Margin.Bottom;
            window.Height = Math.Min(height, double.IsNaN(window.MaxHeight) ? height : window.MaxHeight);
            Vector sizeFix = new Vector(width - window.Width, height - window.Height) / 2;
            Vector marginOffset = new Vector(window.Margin.Left, window.Margin.Top);

            var topLeft = screen.TransformFromDevice.Transform(screen.WorkingArea.TopLeft().ToWPF())
                            + marginOffset + sizeFix;
            window.Left = topLeft.X;
            window.Top = topLeft.Y;
        }
    }
}
