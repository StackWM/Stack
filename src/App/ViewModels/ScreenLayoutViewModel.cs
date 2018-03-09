namespace LostTech.Stack.ViewModels
{
    using LostTech.Windows;

    sealed class ScreenLayoutViewModel : SimpleViewModel
    {
        Win32Screen screen;
        public Win32Screen Screen {
            get => this.screen;
            set => this.Set(ref this.screen, value);
        }

        bool showHints;
        public bool ShowHints {
            get => this.showHints;
            set => this.Set(ref this.showHints, value);
        }
    }
}
