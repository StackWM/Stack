namespace LostTech.Stack.ViewModels
{
    using System;
    using System.Threading.Tasks;
    using System.Windows;
    using LostTech.Stack.Models;

    class DesignTimeWindow : IAppWindow
    {
        public string Title { get; } = nameof(DesignTimeWindow);
        public Rect Bounds => new Rect();
        public bool IsMinimized { get; } = false;
        public bool IsResizable { get; } = true;
        public bool IsVisible { get; } = true;
        public bool IsOnCurrentDesktop { get; } = true;
        public Task<Exception> Activate() => throw new NotSupportedException();
        public Task<Exception> BringToFront() => throw new NotSupportedException();
        public Task<Exception> Move(Rect targetBounds) => throw new NotSupportedException();
    }
}
