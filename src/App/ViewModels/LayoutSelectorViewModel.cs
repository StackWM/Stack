namespace LostTech.Stack.ViewModels
{
    using System.Collections.Generic;
    using LostTech.Stack.Settings;
    using LostTech.Windows;

    class LayoutSelectorViewModel: SimpleViewModel
    {
        string selected;
        public IEnumerable<string> Layouts { get; set; }
        public string ScreenName { get; set; }
        internal ScreenLayouts Settings { get; set; }
        internal Win32Screen Screen { get; set; }

        public string Selected {
            get => this.selected;
            set {
                if (value == this.selected)
                    return;
                this.selected = value;
                if (this.Screen != null)
                    this.Settings?.SetPreferredLayout(this.Screen, value + ".xaml");
                this.OnPropertyChanged();
            }
        }
    }
}
