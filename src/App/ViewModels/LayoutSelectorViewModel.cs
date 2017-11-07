namespace LostTech.Stack.ViewModels
{
    using System.Collections.ObjectModel;

    class LayoutSelectorViewModel: SimpleViewModel
    {
        string selected;
        public ObservableCollection<string> Layouts { get; set; }
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
