namespace LostTech.Stack.Utils
{
    using System.Windows;

    public static class RectExtensions
    {
        public static Point Center(this Rect rect)
        {
            var vector = 0.5 * new Vector(rect.Left + rect.Right, rect.Top + rect.Bottom);
            return new Point(vector.X, vector.Y);
        }

        public static bool Equals(this Point value, Point other, double epsilon)
        {
            return (value - other).LengthSquared < epsilon * epsilon;
        }

        public static double DotProduct(this Vector value, Vector other) => value.X * other.X + value.Y * other.Y;
    }
}
