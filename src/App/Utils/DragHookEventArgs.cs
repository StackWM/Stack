namespace LostTech.Stack.Utils
{
    using System;
    public class DragHookEventArgs : EventArgs
    {
        public DragHookEventArgs(int x, int y)
        {
            this.X = x;
            this.Y = y;
        }
        public int X { get; }
        public int Y { get; }
        public bool Handled { get; set; }
    }
}
