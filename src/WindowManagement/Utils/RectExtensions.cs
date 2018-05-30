namespace LostTech.Stack.Utils {
    using System.Drawing;
    using Rect = System.Drawing.RectangleF;

    public static class RectExtensions {
        public static PointF Center(this Rect rect) =>
            new PointF(0.5f * (rect.Left + rect.Right), 0.5f * (rect.Top + rect.Bottom));

        public static Rect Intersection(this Rect rect, Rect otherRect) {
            rect.Intersect(otherRect);
            return rect;
        }

        public static double Area(this Rect rect) => rect.Width * rect.Height;

        public static double DotProduct(this PointF value, PointF other) => value.X * other.X + value.Y * other.Y;

        public static Rect Inflated(this Rect rect, float x, float y) {
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
            new[] { rect.TopLeft(), rect.TopRight(), rect.BottomRight(), rect.BottomLeft() };
    }
}
