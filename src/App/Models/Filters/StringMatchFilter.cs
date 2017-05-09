namespace LostTech.Stack.Models.Filters
{
    using System.Collections.Generic;
    using System.Linq;
    using LostTech.Stack.DataBinding;

    public abstract class StringMatchFilter<T> : NotifyPropertyChangedBase, IFilter<T>
    {
        string value;

        public string Value
        {
            get => this.value;
            set {
                if (value == this.value)
                    return;
                this.value = value;
                this.OnPropertyChanged();
            }
        }

        public abstract bool Matches(T value);
    }
}
