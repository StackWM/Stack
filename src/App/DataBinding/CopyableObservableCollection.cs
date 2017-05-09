namespace LostTech.Stack.DataBinding
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using LostTech.App;

    public sealed class CopyableObservableCollection<T> : ObservableCollection<T>, ICopyable<CopyableObservableCollection<T>>
        where T:ICopyable<T>
    {
        public CopyableObservableCollection() { }
        public CopyableObservableCollection(IEnumerable<T> values) : base(values.Select(value => value.Copy())) { }
        public CopyableObservableCollection<T> Copy() => new CopyableObservableCollection<T>(this);
    }
}
