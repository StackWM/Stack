namespace LostTech.Stack.Utils
{
    using System.Drawing;
    using System.Windows;
    using Point = System.Windows.Point;
    using Rect = System.Drawing.RectangleF;

    public static class RectExtensions
    {
        public static Point Center(this Rect rect)
        {
            var vector = 0.5 * new Vector(rect.Left + rect.Right, rect.Top + rect.Bottom);
            return new Point(vector.X, vector.Y);
        }

        public static Rect Intersection(this Rect rect, Rect otherRect) {
            rect.Intersect(otherRect);
            return rect;
        }

        public static double Area(this Rect rect) => rect.Width * rect.Height;

        public static bool Equals(this Point value, Point other, double epsilon)
        {
            return (value - other).LengthSquared < epsilon * epsilon;
        }

        public static double DotProduct(this Vector value, Vector other) => value.X * other.X + value.Y * other.Y;

        public static Rect Inflated(this Rect rect, float x, float y)
        {
            if (x < 0 && rect.Width < x)
                return rect;
            if (y < 0 && rect.Height < y)
                return rect;
            rect.Inflate(x, y);
            return rect;
        }

        public static bool IsHorizontal(this Rect rect) => rect.Width > rect.Height;
        public static PointF TopLeft(this Rect rect) => rect.Location;
        public static PointF TopRight(this Rect rect) => new PointF(rect.Right, rect.Top);
        public static PointF BottomRight(this Rect rect) => new PointF(rect.Right, rect.Bottom);
        public static PointF BottomLeft(this Rect rect) => new PointF(rect.Left, rect.Bottom);

        public static PointF[] Corners(this Rect rect) =>
            new[] {rect.TopLeft(), rect.TopRight(), rect.BottomRight(), rect.BottomLeft()};
    }
}
