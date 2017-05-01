namespace LostTech.Stack.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using LostTech.App;
    using LostTech.Stack.DataBinding;
    using LostTech.Stack.Models.Filters;

    public sealed class WindowGroup : NotifyPropertyChangedBase, ICopyable<WindowGroup>
    {
        string name;

        public string Name {
            get => this.name;
            set {
                if (value == this.name)
                    return;
                this.name = value;
                this.OnPropertyChanged();
            }
        }

        public CopyableObservableCollection<WindowFilter> Filters { get; private set; } =
            new CopyableObservableCollection<WindowFilter>();

        public WindowGroup Copy() => new WindowGroup {
            Name = this.Name,
            Filters = this.Filters.Copy(),
        };
    }
}
