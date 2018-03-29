namespace LostTech.Stack.Utils
{
    using System.Windows;
    using DrawingPoint = System.Drawing.Point;

    static class PointUtils
    {
        public static DrawingPoint ToDrawingPoint(this Point point) => new DrawingPoint((int)point.X, (int)point.Y);
    }
}
