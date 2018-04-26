namespace LostTech.Stack.Utils
{
    using System.Windows;
    using PInvoke;
    using DrawingPoint = System.Drawing.Point;

    static class PointUtils
    {
        public static DrawingPoint ToDrawingPoint(this Point point) => new DrawingPoint((int)point.X, (int)point.Y);
        public static Point ToWPF(this POINT point) => new Point(point.x, point.y);
    }
}
