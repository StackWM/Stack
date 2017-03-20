namespace LostTech.Stack.Layouts
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    class Layout
    {
        public ICollection<Zone> Zones { get; } = new ObservableCollection<Zone>();
    }
}
