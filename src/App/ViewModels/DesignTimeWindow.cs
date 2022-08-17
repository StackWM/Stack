namespace LostTech.Stack.ViewModels {
    using System;
    using System.Threading.Tasks;
    using LostTech.Stack.WindowManagement;
    using Rect = System.Drawing.RectangleF;

    class DesignTimeWindow : IAppWindow
    {
        public string Title { get; } = nameof(DesignTimeWindow);
        public Rect Bounds => new Rect();
        public Task<Rect> GetBounds() => Task.FromResult(this.Bounds);
        public Task<Rect> GetClientBounds() => null;
        public bool IsMinimized { get; } = false;
        public bool IsResizable { get; } = true;
        public bool IsVisible { get; } = true;
        public bool IsOnCurrentDesktop { get; } = true;
        public bool IsVisibleInAppSwitcher => true;
        public Task<Exception> Activate() => throw new NotSupportedException();
        public Task<Exception> BringToFront() => throw new NotSupportedException();
        public Task<bool?> Close() => throw new NotSupportedException();
        public Task Move(Rect targetBounds) => throw new NotSupportedException();
        public bool CanMove => false;
        public event EventHandler Closed;
    }
}
