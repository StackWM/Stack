namespace LostTech.Stack.ViewModels
{
    using LostTech.Windows;

    interface ILayoutMappingViewModel
    {
        ScreenLayoutSelector ShowLayoutSelector(Win32Screen screen);
    }
}
