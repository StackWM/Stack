namespace LostTech.Stack.Utils
{
    using LostTech.Windows;

    static class ScreenExtensions
    {
        public static bool IsValidScreen(Win32Screen screen) => screen.IsActive && screen.WorkingArea.Width > 1 && screen.WorkingArea.Height > 1;
    }
}
