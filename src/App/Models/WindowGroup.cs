﻿namespace LostTech.Stack.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Xml.Serialization;
    using LostTech.App.DataBinding;
    using LostTech.Stack.DataBinding;
    using LostTech.Stack.Extensibility.Filters;

    public sealed class WindowGroup : NotifyPropertyChangedBase, ICopyable<WindowGroup>
    {
        public const int LatestVersion = 1;
        string name;
        int version = LatestVersion;

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
        public CopyableObservableCollection<WindowFilter> Filters { get; private set; } =
            new CopyableObservableCollection<WindowFilter>();

        public WindowGroup Copy() => new WindowGroup {
            Name = this.Name,
            Version = this.Version,
            Filters = this.Filters.Copy(),
        };
    }
}
