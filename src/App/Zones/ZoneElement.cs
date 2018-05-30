namespace LostTech.Stack.Zones
{
    using System;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Windows;
    using System.Windows.Data;
    using LostTech.Stack.ViewModels;
    using LostTech.Stack.WindowManagement;

    public class ZoneElement : CollectionContainer
    {
        public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
            nameof(Content), typeof(ZoneViewModel), typeof(ZoneElement),
            new FrameworkPropertyMetadata(null, OnContentChanged));

        static ZoneElement() {
            CollectionProperty.OverrideMetadata(typeof(ZoneElement),
                new FrameworkPropertyMetadata { CoerceValueCallback = CoerceCollection });
        }

        static object CoerceCollection(DependencyObject d, object baseValue) {
            return ((ZoneElement)d).content;
        }

        void OnContentChanged(DependencyPropertyChangedEventArgs change) {
            this.ResetItems();

            if (change.OldValue is ZoneViewModel old)
                old.Windows.CollectionChanged -= this.WindowCollectionChanged;
            if (change.NewValue is ZoneViewModel @new)
                @new.Windows.CollectionChanged += this.WindowCollectionChanged;
        }

        void WindowCollectionChanged(object sender, NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs) {
            this.ResetItems();
        }

        void ResetItems() {
            IAppWindow @new = this.Content?.Windows?.FirstOrDefault()?.Window;

            if (this.content.Count == 0 && @new != null)
                this.content.Add(this.Content);
            else if (this.content.Count != 0 && @new == null)
                this.content.RemoveAt(0);
            else if (@new != null && this.content[0] != this.Content)
                this.content[0] = this.Content;
        }

        protected static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            ((ZoneElement)d).OnContentChanged(e);

        readonly ObservableCollection<ZoneViewModel> content;

        public ZoneElement() {
            this.content = new ObservableCollection<ZoneViewModel>();
            this.CoerceValue(CollectionProperty);
        }

        public ZoneViewModel Content {
            get => (ZoneViewModel)this.GetValue(ContentProperty);
            set => this.SetValue(ContentProperty, value);
        }
    }
}
