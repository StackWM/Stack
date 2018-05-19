namespace LostTech.Stack.Models
{
    using System;
    using System.Threading.Tasks;
    using System.Windows;

    public interface IAppWindow
    {
        Task Move(Rect targetBounds);
        bool CanMove { get; }
        Rect Bounds { get; }
        string Title { get; }
        Task<Exception> Activate();
        Task<Exception> BringToFront();
        bool IsMinimized { get; }
        bool IsResizable { get; }
        bool IsVisible { get; }
        bool IsOnCurrentDesktop { get; }
        bool IsVisibleOnAllDesktops { get; }
        event EventHandler Closed;
    }
}
