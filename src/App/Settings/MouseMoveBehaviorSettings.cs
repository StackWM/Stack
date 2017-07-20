namespace LostTech.Stack.Settings
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using LostTech.App.DataBinding;

    public sealed class MouseMoveBehaviorSettings : NotifyPropertyChangedBase, ICopyable<MouseMoveBehaviorSettings>
    {
        bool enabled = true;

        public bool Enabled {
            get => this.enabled;
            set {
                this.enabled = value;
                this.OnPropertyChanged();
            }
        }

        public ObservableCollection<string> WindowGroupIgnoreList { get; } =
            new ObservableCollection<string>();

        public MouseMoveBehaviorSettings Copy()
        {
            var copy = new MouseMoveBehaviorSettings {
                Enabled = this.Enabled,
            };
            foreach (string groupName in this.WindowGroupIgnoreList)
                copy.WindowGroupIgnoreList.Add(groupName);
            return copy;
        }
    }
}