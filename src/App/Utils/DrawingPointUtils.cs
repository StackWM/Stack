namespace LostTech.Stack.Utils {
    using System.Drawing;
    static class DrawingPointUtils {
        public static PointF Diff(this PointF self, PointF other) => new PointF(self.X - other.X, self.Y - other.Y);
        public static float LengthSquared(this PointF self) => self.X * self.X + self.Y * self.Y;
    }
}
