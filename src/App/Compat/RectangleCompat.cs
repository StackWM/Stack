namespace LostTech.Stack.Compat
{
    using System.Windows;

    static class RectangleCompat
    {
        public static Rect ToWPF(this System.Drawing.Rectangle formsRectangle) =>
            new Rect(x: formsRectangle.X, y: formsRectangle.Y,
                width: formsRectangle.Width, height: formsRectangle.Height);
        public static Point ToWPF(this System.Drawing.Point point) => new Point(point.X, point.Y);
    }
}
