namespace LostTech.Stack.ViewModels
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using LostTech.Stack.Models;

    public class ZoneViewModel : SimpleViewModel
    {
        public ObservableCollection<IAppWindow> Windows { get; internal set; }
        public string Id { get; internal set; }
    }
}
