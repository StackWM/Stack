namespace LostTech.Stack.Compat
{
    using System.Drawing;
    using System.Windows;

    static class RectangleCompat
    {
        public static Rect ToWPF(this Rectangle formsRectangle) =>
            new Rect(x: formsRectangle.X, y: formsRectangle.Y,
                width: formsRectangle.Width, height: formsRectangle.Height);
    }
}
