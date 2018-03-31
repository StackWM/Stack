namespace LostTech.Stack.Models
{
    using System;
    using System.Threading.Tasks;
    using System.Windows;

    public interface IAppWindow
    {
        Task<Exception> Move(Rect targetBounds);
        Rect Bounds { get; }
        string Title { get; }
        Task<Exception> Activate();
        Task<Exception> BringToFront();
        bool IsMinimized { get; }
    }
}
