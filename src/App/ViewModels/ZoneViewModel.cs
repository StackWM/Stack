namespace LostTech.Stack.ViewModels
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    public class ZoneViewModel : SimpleViewModel
    {
        public ObservableCollection<AppWindowViewModel> Windows { get; internal set; }
        public string Id { get; internal set; }
    }
}
