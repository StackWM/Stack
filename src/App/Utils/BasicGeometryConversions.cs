namespace LostTech.Stack.Utils
{
    using System.Drawing;
    using System.Windows;
    using PInvoke;
    using DrawingPoint = System.Drawing.Point;
    using WpfPoint = System.Windows.Point;

    static class BasicGeometryConversions
    {
        public static PointF ToDrawingPoint(this WpfPoint point) => new DrawingPoint((int)point.X, (int)point.Y);
        public static DrawingPoint ToDrawingPoint(this POINT point) => new DrawingPoint(point.x, point.y);
        public static WpfPoint ToWPF(this PointF point) => new WpfPoint(point.X, point.Y);
        public static Vector AsWPFVector(this SizeF size) => new Vector(size.Width, size.Height);
    }
}
