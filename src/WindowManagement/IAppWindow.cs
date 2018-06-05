namespace LostTech.Stack.WindowManagement
{
    using System;
    using System.Threading.Tasks;
    using Rect = System.Drawing.RectangleF;

    public interface IAppWindow
    {
        Task Move(Rect targetBounds);
        Task<bool?> Close();
        bool CanMove { get; }
        Rect Bounds { get; }
        Task<Rect> GetBounds();
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
