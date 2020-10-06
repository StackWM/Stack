namespace LostTech.Stack.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Xml.Serialization;
    using LostTech.App.DataBinding;
    using LostTech.Stack.DataBinding;
    using LostTech.Stack.Extensibility.Filters;
    using LostTech.Stack.WindowManagement;
    using ThomasJaworski.ComponentModel;

    public sealed class WindowGroup : NotifyPropertyChangedBase, ICopyable<WindowGroup>,
        IWindowGroup, IFilter<IAppWindow>
    {
        public const int LatestVersion = 1;
        string name;
        int version = LatestVersion;
        readonly ChangeListener changeListener;

        public WindowGroup() {
            this.changeListener = ChangeListener.Create(this.Filters);
            this.changeListener.CollectionChanged += this.UpdateFiltersStringHander;
            this.changeListener.PropertyChanged += this.UpdateFiltersStringHander;
        }

        internal bool VersionSetExplicitly { get; private set; }

        [XmlAttribute]
        public string Name {
            get => this.name;
            set {
                if (value == this.name)
                    return;
                this.name = value;
                this.OnPropertyChanged();
            }
        }

        [DefaultValue(0)]
        [XmlAttribute]
        public int Version {
            get => this.version;
            set {
                this.VersionSetExplicitly = true;
                if (value == this.version)
                    return;

                this.version = value;
                this.OnPropertyChanged();
            }
        }

        [XmlElement("Filter")]
        public CopyableObservableCollection<WindowFilter> Filters { get; } =
            new CopyableObservableCollection<WindowFilter>();

        [XmlIgnore]
        public string FiltersString => string.Join(Environment.NewLine, this.Filters);

        public bool Matches(IAppWindow value)
            => this.Filters?.Any(f => f.Matches(((Win32Window)value).Handle)) == true;

        public WindowGroup Copy() {
            var copy = new WindowGroup {
                Name = this.Name,
                Version = this.Version,
            };
            this.Filters.CopyTo(copy.Filters);
            return copy;
        }

        void UpdateFiltersStringHander(object sender, EventArgs e) => this.OnPropertyChanged(nameof(this.FiltersString));
    }
}
