namespace LostTech.Stack.Models
{
    using System;
    using LostTech.Stack.Utils;
    using LostTech.Stack.WindowManagement;

    interface IWindowTracker
    {
        event EventHandler<EventArgs<IAppWindow>> WindowAppeared;
    }
}
