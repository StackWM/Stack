namespace LostTech.Stack.ViewModels
{
    using LostTech.Stack.ScreenTracking;
    using LostTech.Windows;

    sealed class ScreenLayoutViewModel : SimpleViewModel, IScreenLayoutViewModel
    {
        Win32Screen screen;
        public Win32Screen Screen {
            get => this.screen;
            set => this.Set(ref this.screen, value);
        }
    }
}
