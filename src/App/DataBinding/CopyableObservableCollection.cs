namespace LostTech.Stack.DataBinding
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using JetBrains.Annotations;
    using LostTech.App.DataBinding;

    public sealed class CopyableObservableCollection<T> : ObservableCollection<T>, ICopyable<CopyableObservableCollection<T>>
        where T:ICopyable<T>
    {
        public CopyableObservableCollection() { }
        public CopyableObservableCollection(IEnumerable<T> values) : base(values.Select(value => value.Copy())) { }
        public CopyableObservableCollection<T> Copy() => new CopyableObservableCollection<T>(this);
        public void CopyTo([NotNull] ICollection<T> target) {
            if (target == null) throw new ArgumentNullException(nameof(target));
            foreach (var val in this.Select(value => value.Copy()))
                target.Add(val);
        }
    }
}
