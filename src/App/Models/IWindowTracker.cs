namespace LostTech.Stack.Models
{
    using System;
    using LostTech.Stack.Utils;

    interface IWindowTracker
    {
        event EventHandler<EventArgs<IAppWindow>> WindowAppeared;
    }
}
