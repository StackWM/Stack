namespace LostTech.Stack.ViewModels
{
    using System;
    using System.Threading.Tasks;
    using System.Windows;
    using LostTech.Stack.Models;

    class DesignTimeWindow : IAppWindow
    {
        public string Title { get; } = nameof(DesignTimeWindow);
        public Task<Exception> Activate() => throw new NotSupportedException();
        public Task<Exception> Move(Rect targetBounds) => throw new NotSupportedException();
    }
}
