namespace LostTech.Stack.DataBinding
{
    using System;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq.Expressions;

    static class Binder
    {
        public static void OnChange<TPropertyType, TSource>(this TSource dataSource, Expression<Func<TSource, TPropertyType>> property, Action<TPropertyType> action)
            where TSource : INotifyPropertyChanged
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            if (dataSource == null)
                throw new ArgumentNullException(nameof(dataSource));
            if (property == null)
                throw new ArgumentNullException(nameof(property));
            if (!(property.Body is MemberExpression sourceMember))
                throw new ArgumentException(message: "Lambda must be a property access expression", paramName: nameof(property));

            var getter = property.Compile();
            string propertyName = sourceMember.Member.Name;
            dataSource.PropertyChanged += (_, args) => {
                if (args.PropertyName == propertyName)
                    action(getter(dataSource));
            };
            action(getter(dataSource));
        }

        public static void OnChange<T>(this INotifyCollectionChanged collection, Action<T> onAdd, Action<T> onRemove)
        {
            collection.CollectionChanged += (_, args) => {
                switch (args.Action) {
                case NotifyCollectionChangedAction.Add:
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Replace:
                    foreach (T item in args.NewItems ?? new T[0])
                        onAdd(item);
                    foreach (T item in args.OldItems ?? new T[0])
                        onRemove(item);
                    return;
                case NotifyCollectionChangedAction.Reset:
                    throw new NotSupportedException();
                case NotifyCollectionChangedAction.Move:
                default:
                    return;
                }
            };
        }
    }
}