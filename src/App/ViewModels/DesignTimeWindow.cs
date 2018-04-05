namespace LostTech.Stack.ViewModels
{
    using System;
    using System.Threading.Tasks;
    using LostTech.Stack.WindowManagement;
    using Rect = System.Drawing.RectangleF;

    class DesignTimeWindow : IAppWindow
    {
        public string Title { get; } = nameof(DesignTimeWindow);
        public Rect Bounds => new Rect();
        public bool IsMinimized { get; } = false;
        public bool CanMove { get; } = false;
        public bool IsResizable { get; } = false;
        public bool IsVisible { get; } = true;
        public bool IsOnCurrentDesktop { get; } = true;
        public bool IsVisibleOnAllDesktops { get; } = false;

        public event EventHandler Closed;

        public Task<Exception> Activate() => throw new NotSupportedException();
        public Task<Exception> BringToFront() => throw new NotSupportedException();
        public Task<bool?> Close() => throw new NotImplementedException();
        public Task<Rect> GetBounds() => throw new NotImplementedException();
        public Task Move(Rect targetBounds) => throw new NotSupportedException();
    }
}
