namespace LostTech.Stack.ViewModels
{
    using System.Collections.Generic;
    using LostTech.Windows;

    class LayoutSelectorViewModel: SimpleViewModel
    {
        string selected;
        public IEnumerable<string> Layouts { get; set; }
        public string ScreenName { get; set; }

        public string Selected {
            get => this.selected;
            set {
                if (value == this.selected)
                    return;
                this.selected = value;
                this.OnPropertyChanged();
            }
        }
    }
}
