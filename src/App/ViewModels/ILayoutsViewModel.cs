namespace LostTech.Stack.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using LostTech.Stack.Utils;

    interface ILayoutsViewModel
    {
        IEnumerable<ScreenLayout> ScreenLayouts { get; }
        Task ReloadLayout(ScreenLayout screenLayout, bool force);
        event EventHandler<EventArgs<ScreenLayout>> LayoutLoaded;
    }
}
