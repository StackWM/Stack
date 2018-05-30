namespace LostTech.Stack.Utils {
    using System;
    using System.Drawing;
    public static class DrawingPointUtils {
        public static PointF Diff(this PointF self, PointF other) => new PointF(self.X - other.X, self.Y - other.Y);
        public static float LengthSquared(this PointF self) => self.X * self.X + self.Y * self.Y;
        public static float Length(this PointF self) => (float)Math.Sqrt(self.LengthSquared());

        public static bool Equals(this PointF value, PointF other, double epsilon) =>
            value.Diff(other).LengthSquared() < epsilon * epsilon;
        public static PointF Scale(this PointF self, float scale) =>
            new PointF(scale * self.X, scale * self.Y);
    }
}
