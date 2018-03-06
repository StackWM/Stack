namespace LostTech.Stack.Models
{
    using System;
    using System.Threading.Tasks;
    using System.Windows;

    public interface IAppWindow
    {
        Task<Exception> Move(Rect targetBounds);
        string Title { get; }
        Task<Exception> Activate();
    }
}
