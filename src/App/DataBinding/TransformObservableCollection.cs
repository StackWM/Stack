namespace LostTech.Stack.DataBinding
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Linq;

    public class TransformObservableCollection<T, TSource, TCollection> : 
        INotifyCollectionChanged, IList, IReadOnlyList<T>, IDisposable
        where TCollection: class, IReadOnlyCollection<TSource>, INotifyCollectionChanged
    {
        public TransformObservableCollection(TCollection wrappedCollection, Func<TSource, T> transform)
        {
            this.wrappedCollection = wrappedCollection;
            this.transform = transform;
            this.wrappedCollection.CollectionChanged += this.TransformObservableCollection_CollectionChanged;
            this.transformedCollection = new ObservableCollection<T>(this.wrappedCollection.Select(this.transform));
        }
        public void Dispose()
        {
            if (this.wrappedCollection == null) return;
            this.wrappedCollection.CollectionChanged -= this.TransformObservableCollection_CollectionChanged;
            this.wrappedCollection = null;
        }
        void TransformObservableCollection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action) {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems == null || e.NewItems.Count != 1)
                    break;
                this.transformedCollection.Insert(e.NewStartingIndex, this.transform((TSource)e.NewItems[0]));
                return;
            case NotifyCollectionChangedAction.Move:
                if (e.NewItems == null || e.NewItems.Count != 1 || e.OldItems == null || e.OldItems.Count != 1)
                    break;
                this.transformedCollection.Move(e.OldStartingIndex, e.NewStartingIndex);
                return;
            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems == null || e.OldItems.Count != 1)
                    break;
                this.transformedCollection.RemoveAt(e.OldStartingIndex);
                return;
            case NotifyCollectionChangedAction.Replace:
                if (e.NewItems == null || e.NewItems.Count != 1 || e.OldItems == null || e.OldItems.Count != 1 || e.OldStartingIndex != e.NewStartingIndex)
                    break;
                this.transformedCollection[e.OldStartingIndex] = this.transform((TSource)e.NewItems[0]);
                return;
            } // This  is most likely called on a Clear(), we don't optimize the other cases (yet)
            this.transformedCollection.Clear();
            foreach (var item in this.wrappedCollection)
                this.transformedCollection.Add(this.transform(item));
        }

        #region IList Edit functions that are unsupported because this collection is read only
        public int Add(object value) { throw new InvalidOperationException(); }
        public void Clear() { throw new InvalidOperationException(); }
        public void Insert(int index, object value) { throw new InvalidOperationException(); }
        public void Remove(object value) { throw new InvalidOperationException(); }
        public void RemoveAt(int index) { throw new InvalidOperationException(); }
        #endregion IList Edit functions that are unsupported because this collection is read only

        #region Accessors
        public T this[int index] => this.transformedCollection[index];

        object IList.this[int index] { get => this.transformedCollection[index];
            set => throw new InvalidOperationException();
        }
        public bool Contains(T value) { return this.transformedCollection.Contains(value); }
        bool IList.Contains(object value) { return ((IList)this.transformedCollection).Contains(value); }
        public int IndexOf(T value) { return this.transformedCollection.IndexOf(value); }
        int IList.IndexOf(object value) { return ((IList)this.transformedCollection).IndexOf(value); }
        public int Count => this.transformedCollection.Count;
        public IEnumerator<T> GetEnumerator() { return this.transformedCollection.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return ((IEnumerable)this.transformedCollection).GetEnumerator(); }
        #endregion Accessors

        public bool IsFixedSize => false;
        public bool IsReadOnly => true;

        public void CopyTo(Array array, int index) {
            ((IList)this.transformedCollection).CopyTo(array, index);
        }
        public void CopyTo(T[] array, int index) {
            this.transformedCollection.CopyTo(array, index); }
        public bool IsSynchronized => false;
        public object SyncRoot => this.transformedCollection;

        readonly ObservableCollection<T> transformedCollection;
        TCollection wrappedCollection;
        readonly Func<TSource, T> transform;

        event NotifyCollectionChangedEventHandler INotifyCollectionChanged.CollectionChanged {
            add => ((INotifyCollectionChanged)this.transformedCollection).CollectionChanged += value;
            remove => ((INotifyCollectionChanged)this.transformedCollection).CollectionChanged -= value;
        }
    }
}
