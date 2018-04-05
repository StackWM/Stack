namespace LostTech.Stack.ViewModels
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    interface ILayoutsViewModel
    {
        IEnumerable<ScreenLayout> ScreenLayouts { get; }
        Task ReloadLayout(ScreenLayout screenLayout);
    }
}
